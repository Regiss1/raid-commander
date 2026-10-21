using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RaidCommander.Combat;
using RaidCommander.Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace RaidCommander.Verification
{
    /// <summary>Exercises actual damage outcomes and player-frame attack scheduling, using runtime stats copies.</summary>
    public static class AttackSpeedVerification
    {
        [MenuItem("Raid Commander/Verify Protected Combat and Attack Speed in Play Mode")]
        public static void Begin() => PartyAiVerification.Begin();

        public static IEnumerator Verify()
        {
            PartyAiVerification.SuspendAutonomy();
            var combat = UnityEngine.Object.FindAnyObjectByType<CombatSystem>();
            var actions = UnityEngine.Object.FindAnyObjectByType<CombatDebugActions>();
            CombatUnit tank = Unit("Tank"), healer = Unit("Healer"), melee = Unit("DPS"), ranged = Unit("Ranged DPS"), boss = Unit("Raid Boss");
            CombatUnit[] party = { tank, healer, melee, ranged };
            CombatUnit[] units = { tank, healer, melee, ranged, boss };
            var bossAgent = boss.GetComponent<NavMeshAgent>();
            bool bossAgentEnabled = bossAgent.enabled;
            bossAgent.enabled = false; // Coordinate fixtures isolate attack timing from navigation resnapping.
            Vector3[] positions = units.Select(u => u.transform.position).ToArray();
            int tankMaximum = tank.MaximumHealth;
            var originals = new List<CombatStatsDefinition>();
            var copies = new List<CombatStatsDefinition>();
            var outcomes = new List<(ResolvedCombatEvent hit, float time, float delta)>();
            Action<ResolvedCombatEvent> observer = hit => outcomes.Add((hit, Time.time, Time.deltaTime));
            combat.EventResolved += observer;
            GameObject fallbackObject = null;
            try
            {
                for (int i = 0; i < party.Length; i++)
                {
                    var settings = new SerializedObject(party[i]);
                    var original = (CombatStatsDefinition)settings.FindProperty("stats").objectReferenceValue;
                    Check(original != null && original.AttackSpeed > 0f && !float.IsInfinity(party[i].AttackSpeed), party[i].name + " saved attack speed is invalid.");
                    Check(settings.FindProperty("fallbackAttackSpeed").floatValue == 1f, party[i].name + " fallback attack speed is not 1.");
                    originals.Add(original);
                    var copy = UnityEngine.Object.Instantiate(original);
                    copy.hideFlags = HideFlags.DontSave;
                    copies.Add(copy);
                    SetSpeed(copy, 1f);
                    settings.FindProperty("stats").objectReferenceValue = copy;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
                fallbackObject = new GameObject("Attack speed fallback verification");
                var fallback = fallbackObject.AddComponent<CombatUnit>();
                Check(fallback.AttackSpeed == 1f, "Default statless unit attack speed is not 1.");
                foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    SetSpeed(copies[0], invalid);
                    Check(tank.AttackSpeed == 1f, "Invalid stats attack speed was not sanitized.");
                    var settings = new SerializedObject(fallback);
                    settings.FindProperty("fallbackAttackSpeed").floatValue = invalid;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    Check(fallback.AttackSpeed == 1f, "Invalid fallback attack speed was not sanitized.");
                }
                SetSpeed(copies[0], float.Epsilon);
                Check(tank.AttackSpeed == 0.01f && !float.IsInfinity(1f / tank.AttackSpeed), "Tiny positive attack speed produced an infinite interval.");
                SetSpeed(copies[0], 1f);

                Reset();
                combat.ApplyDamage(null, boss, boss.CurrentHealth - 5);
                outcomes.Clear();
                int logBefore = combat.LogService.History.Count;
                combat.KeepBossAlive = true;
                Check(combat.TryRangedAttack(ranged, boss, out CombatActionFailure failure, out int reported) && reported == ranged.BasicDamage,
                    "Threshold-crossing protected ranged attack failed.");
                Check(boss.CurrentHealth == 1 && boss.IsAlive && outcomes.Count == 1 && outcomes[0].hit.Damage == ranged.BasicDamage,
                    "Protected threshold hit did not preserve health and report normal damage.");
                Check(combat.LogService.History.Count == logBefore + 1 && combat.LogService.History.Last().Damage == ranged.BasicDamage,
                    "Protected hit was not logged exactly once.");
                long sequence = combat.LogService.History.Last().Sequence;
                int changes = 0;
                Action<CombatUnit> changed = _ => changes++;
                boss.HealthChanged += changed;
                try
                {
                    for (int i = 0; i < 3; i++)
                        Check(combat.TryRangedAttack(ranged, boss, out failure, out reported) && reported == ranged.BasicDamage,
                            "Repeated protected hit was rejected.");
                    Check(changes == 0, "Zero health loss emitted a health change.");
                }
                finally { boss.HealthChanged -= changed; }
                Check(boss.CurrentHealth == 1 && outcomes.Count == 4 && outcomes.All(o => o.hit.Type == CombatEventType.RangedDamage),
                    "Protected damage generated a death event or lost a hit.");
                Check(ranged.GetComponent<LineRenderer>().enabled, "Protected ranged hit did not trigger its tracer.");
                var last = combat.LogService.History.Last();
                Check(last.Sequence == sequence + 3 && last.SourceId == ranged.EncounterId && last.TargetId == boss.EncounterId && last.Timestamp >= 0,
                    "Protected damage event metadata/sequence changed.");
                actions.RangedDpsAttack();
                Check(actions.LastStatus.Contains("for " + ranged.BasicDamage + "."), "Debug ranged feedback reports health loss instead of hit damage.");
                actions.TankMelee();
                Check(actions.LastStatus.Contains("for " + tank.BasicDamage + "."), "Debug melee feedback reports zero protected damage.");
                actions.KillBoss();
                Check(boss.IsAlive && boss.CurrentHealth == 1 && actions.LastStatus.Contains("protection"), "Protected debug kill reported a defeat.");
                combat.KeepBossAlive = false;
                Check(combat.TryRangedAttack(ranged, boss, out failure, out reported) && reported == 1 && !boss.IsAlive,
                    "Unprotected lethal hit lost effective-damage semantics.");
                Check(outcomes.Count(o => o.hit.Type == CombatEventType.UnitDied) == 1, "Normal lethal hit did not report exactly one death.");
                combat.KeepBossAlive = true;
                Check(!combat.TryRangedAttack(ranged, boss) && !boss.IsAlive, "Protection revived a dead boss.");

                Reset();
                combat.KeepBossAlive = true;
                combat.ApplyDamage(null, boss, int.MaxValue);
                boss.transform.position = new Vector3(-5f, positions[4].y, 7f);
                Warp(ranged, new Vector3(-5f, 0f, -1f));
                outcomes.Clear();
                logBefore = combat.LogService.History.Count;
                Check(!combat.TryRangedAttack(ranged, boss, out failure) && failure == CombatActionFailure.LineOfSight,
                    "Protected boss accepted a blocked ranged attack.");
                Check(outcomes.Count == 0 && combat.LogService.History.Count == logBefore, "Failed protected request generated an outcome.");
                Warp(ranged, new Vector3(-14f, 0f, -14f));
                Check(!combat.TryRangedAttack(ranged, boss), "Protected boss accepted an out-of-range attack.");
                combat.ApplyDamage(null, ranged, int.MaxValue);
                int count = outcomes.Count;
                Check(!combat.TryRangedAttack(ranged, boss) && outcomes.Count == count, "Dead source attacked a protected boss.");
                Debug.Log("ATTACK SPEED CHECK: protected damage, log metadata, tracer, debug feedback and normal death passed.");

                foreach (int actorIndex in new[] { 0, 2, 3 })
                {
                    CombatUnit actor = party[actorIndex];
                    foreach (float speed in new[] { 0.5f, 1f, 2f, 7f })
                    {
                        Reset();
                        SetSpeed(copies[actorIndex], speed);
                        combat.KeepBossAlive = true;
                        combat.ApplyDamage(null, boss, int.MaxValue);
                        outcomes.Clear();
                        actor.GetComponent<PartyRoleBrain>().enabled = true;
                        float deadline = Time.time + 4f / speed + 3f;
                        while (Hits(actor).Length < 4)
                        { Check(Time.time < deadline, actor.name + " stopped attacking at the health floor with speed " + speed); yield return null; }
                        var hits = Hits(actor);
                        CheckIntervals(hits, 1f / speed, actor.name + " at " + speed + " attacks/sec");
                        Check(boss.IsAlive && boss.CurrentHealth == 1 && hits.All(h => h.hit.Damage == actor.BasicDamage), "Automated protected hit lost normal damage.");
                        Check(actor.GetComponent<PartyRoleBrain>().State != RoleState.Idle, "Protected boss put attacker into idle state.");
                        if (actor == ranged && speed == 0.5f)
                        {
                            count = outcomes.Count;
                            actions.RangedDpsAttack(); actions.RangedDpsAttack(); actions.RangedDpsAttack();
                            Check(outcomes.Count == count + 3, "Debug ranged buttons acquired an automated cooldown.");
                        }
                        if (actor == tank && speed == 0.5f)
                        {
                            count = outcomes.Count;
                            actions.TankMelee(); actions.TankMelee(); actions.TankMelee();
                            Check(outcomes.Count == count + 3, "Debug melee buttons acquired an automated cooldown.");
                        }
                        actor.GetComponent<PartyRoleBrain>().enabled = false;
                        SetSpeed(copies[actorIndex], 1f);
                    }
                }

                Reset();
                tank.SetMaximumHealth(1000);
                combat.ResetSession(units);
                combat.ApplyDamage(null, tank, 700);
                SetSpeed(copies[1], 7f);
                outcomes.Clear();
                healer.GetComponent<PartyRoleBrain>().enabled = true;
                float until = Time.time + 8f;
                while (Heals().Length < 4) { Check(Time.time < until, "Healer did not sustain basic healing."); yield return null; }
                CheckIntervals(Heals(), 1.5f, "Healing independent of attack speed");
                healer.GetComponent<PartyRoleBrain>().enabled = false;
                combat.ApplyDamage(null, tank, 100);
                count = outcomes.Count;
                actions.HealerHeal(); actions.HealerHeal(); actions.HealerHeal();
                Check(outcomes.Count == count + 3, "Debug heals acquired the automated healing interval.");
                SetSpeed(copies[1], 1f);

                Reset();
                Vector3 bossPosition = boss.transform.position;
                var bossAttack = boss.GetComponent<BossBasicAttack>();
                bossAttack.enabled = true;
                until = Time.time + 11f;
                while (Hits(boss).Length < 3) { Check(Time.time < until, "Boss did not attack at its T8 rate."); yield return null; }
                CheckIntervals(Hits(boss), 3f, "T8 boss interval independent of party stats");
                Debug.Log("ATTACK SPEED VERIFY PASS: three party attackers at 0.5/1/2/7 APS, protected hit continuity, separate healing/T8 boss timing and immediate debug actions.");
            }
            finally
            {
                PartyAiVerification.SuspendAutonomy();
                combat.EventResolved -= observer;
                combat.KeepBossAlive = false;
                for (int i = 0; i < originals.Count; i++)
                {
                    var settings = new SerializedObject(party[i]);
                    settings.FindProperty("stats").objectReferenceValue = originals[i];
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    UnityEngine.Object.Destroy(copies[i]);
                }
                tank.SetMaximumHealth(tankMaximum);
                combat.ResetSession(units);
                for (int i = 0; i < 4; i++) Warp(units[i], positions[i]);
                boss.transform.position = positions[4];
                bossAgent.enabled = bossAgentEnabled;
                if (fallbackObject != null) UnityEngine.Object.Destroy(fallbackObject);
            }

            void Reset()
            {
                PartyAiVerification.SuspendAutonomy();
                combat.KeepBossAlive = false;
                combat.ResetSession(units);
                boss.transform.position = positions[4];
                Warp(tank, boss.transform.position + Vector3.back);
                Warp(melee, boss.transform.position + Vector3.back);
                Warp(ranged, boss.transform.position + Vector3.back * 12f);
                Warp(healer, boss.transform.position + Vector3.back * 12f);
                outcomes.Clear();
            }
            (ResolvedCombatEvent hit, float time, float delta)[] Hits(CombatUnit source) => outcomes.Where(o => o.hit.Source == source && o.hit.Damage > 0).ToArray();
            (ResolvedCombatEvent hit, float time, float delta)[] Heals() => outcomes.Where(o => o.hit.Source == healer && o.hit.Healing > 0).ToArray();
        }

        private static void CheckIntervals((ResolvedCombatEvent hit, float time, float delta)[] hits, float expected, string label)
        {
            Check(hits.Length >= 3, "Not enough timed outcomes: " + label);
            for (int i = 1; i < hits.Length; i++)
            {
                float interval = hits[i].time - hits[i - 1].time;
                float tolerance = Mathf.Max(hits[i].delta, hits[i - 1].delta) * 2f + 0.03f;
                Check(interval >= expected - 0.005f && interval <= expected + tolerance,
                    label + ": observed interval " + interval + ", expected " + expected + " within frame tolerance " + tolerance);
            }
        }

        private static CombatUnit Unit(string name) => GameObject.Find(name).GetComponent<CombatUnit>();
        private static void SetSpeed(CombatStatsDefinition stats, float value)
        {
            var settings = new SerializedObject(stats);
            settings.FindProperty("attackSpeed").floatValue = value;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Warp(CombatUnit unit, Vector3 position)
        {
            var movement = unit.GetComponent<RaidCommander.Movement.UnitMovement>();
            movement.Cancel();
            var agent = unit.GetComponent<NavMeshAgent>();
            Check(NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }),
                "Timing fixture is off mesh: " + position);
            Check(agent.Warp(hit.position + Vector3.up * agent.baseOffset), "Could not position " + unit.name);
            agent.ResetPath();
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
