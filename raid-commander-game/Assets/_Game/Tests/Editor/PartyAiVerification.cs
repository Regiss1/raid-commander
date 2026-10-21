using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RaidCommander.Combat;
using RaidCommander.Movement;
using RaidCommander.Selection;
using RaidCommander.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace RaidCommander.Verification
{
    /// <summary>Real player-loop integration checks against the saved arena and normal command owners.</summary>
    [InitializeOnLoad]
    public static class PartyAiVerification
    {
        private const string PendingKey = "RaidCommander.PartyAiVerification.Pending";
        private static IEnumerator checks;
        private static double started;
        private static bool background;
        public static string Result { get; private set; } = "Not run";

        static PartyAiVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
                SessionState.SetBool(PendingKey, false);
                background = Application.runInBackground;
                Application.runInBackground = true;
                started = EditorApplication.timeSinceStartup;
                Result = "Running";
                checks = Verify();
                EditorApplication.update += Tick;
            };
        }

        [MenuItem("Raid Commander/Verify Party AI in Play Mode")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run AI verification from Edit mode.");
            if (SceneManagerDirty()) throw new InvalidOperationException("Save the intended scene before running AI verification.");
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/RaidArena.unity");
            SessionState.SetBool(PendingKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static bool SceneManagerDirty() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty;

        private static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup - started < 200, "AI verification timed out.");
                EditorApplication.QueuePlayerLoopUpdate();
                if (!checks.MoveNext()) Finish(true, "all role, encounter, override, sight, death/reset and debug-floor checks passed");
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        private static void Finish(bool passed, string message)
        {
            EditorApplication.update -= Tick;
            (checks as IDisposable)?.Dispose();
            checks = null;
            Application.runInBackground = background;
            Time.timeScale = 1f;
            Result = (passed ? "PASS: " : "FAIL: ") + message;
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText("Logs/t7-ai-verification.txt", Result);
            Debug.Log("PARTY AI VERIFY " + Result);
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }

        public static void SuspendAutonomy()
        {
            foreach (PartyRoleBrain brain in UnityEngine.Object.FindObjectsByType<PartyRoleBrain>()) brain.enabled = false;
            foreach (BossBasicAttack attack in UnityEngine.Object.FindObjectsByType<BossBasicAttack>()) attack.enabled = false;
        }

        private static IEnumerator Verify()
        {
            SuspendAutonomy();
            Time.timeScale = 3f;
            int frame = Time.frameCount;
            yield return null;
            yield return null;
            while (Time.frameCount <= frame + 2) yield return null;
            Check(Time.time > 0f, "Player loop is not advancing.");
            var combat = UnityEngine.Object.FindAnyObjectByType<CombatSystem>();
            var tank = GameObject.Find("Tank").GetComponent<CombatUnit>();
            var healer = GameObject.Find("Healer").GetComponent<CombatUnit>();
            var melee = GameObject.Find("DPS").GetComponent<CombatUnit>();
            var ranged = GameObject.Find("Ranged DPS").GetComponent<CombatUnit>();
            var boss = GameObject.Find("Raid Boss").GetComponent<CombatUnit>();
            CombatUnit[] units = { tank, healer, melee, ranged, boss };
            CombatUnit[] party = { tank, healer, melee, ranged };
            PartyRoleBrain[] brains = party.Select(u => u.GetComponent<PartyRoleBrain>()).ToArray();
            var bossAttack = boss.GetComponent<BossBasicAttack>();
            Vector3[] original = units.Select(u => u.transform.position).ToArray();
            var events = new List<ResolvedCombatEvent>();
            Action<ResolvedCombatEvent> record = e => events.Add(e);
            combat.EventResolved += record;
            try
            {
                Check(brains.All(b => b != null) && bossAttack != null, "Saved arena is missing AI wiring.");
                Check(!combat.KeepBossAlive, "Boss protection must default off.");
                Reset();
                foreach (PartyRoleBrain brain in brains) brain.enabled = true;
                bossAttack.enabled = true;
                float deadline = Time.time + 45f;
                while (boss.IsAlive)
                {
                    Check(Time.time < deadline, "Unattended encounter did not kill boss. " + Describe());
                    yield return null;
                }
                foreach (CombatUnit attacker in new[] { tank, melee, ranged })
                    Check(events.Any(e => e.Source == attacker && e.Target == boss && e.Damage > 0), attacker.name + " never attacked.");
                Check(events.Any(e => e.Source == healer && e.Target == tank && e.Healing > 0), "Healer did not heal Tank during unattended combat.");
                Check(events.Any(e => e.Source == boss && e.Target == tank && e.Damage > 0), "Boss never attacked Tank.");
                float until = Time.time + 0.5f;
                while (Time.time < until) yield return null;
                Check(brains.All(b => b.State == RoleState.Idle), "Party did not become idle after boss death.");
                int count = events.Count;
                until = Time.time + 2f;
                while (Time.time < until) yield return null;
                Check(events.Count == count, "Combat continued after boss death.");
                Debug.Log("PARTY AI CHECK: unattended encounter and all roles passed.");

                Reset();
                combat.KeepBossAlive = true;
                Check(combat.ApplyDamage(tank, boss, int.MaxValue) == boss.MaximumHealth - 1 && boss.CurrentHealth == 1 && boss.IsAlive,
                    "Boss damage floor failed.");
                count = events.Count;
                Check(combat.ApplyDamage(tank, boss, 20) == 0 && events.Count == count, "Damage at floor produced an effective outcome.");
                UnityEngine.Object.FindAnyObjectByType<CombatDebugActions>().KillBoss();
                Check(boss.CurrentHealth == 1 && !events.Any(e => e.Type == CombatEventType.UnitDied), "Debug kill bypassed boss protection.");
                combat.KeepBossAlive = false;
                combat.ApplyDamage(tank, boss, 1);
                Check(!boss.IsAlive && events.Count(e => e.Type == CombatEventType.UnitDied) == 1, "Disabling protection failed to restore normal death.");

                Reset();
                bossAttack.enabled = true;
                Warp(tank, boss.transform.position + Vector3.back * 8f);
                int health = tank.CurrentHealth;
                until = Time.time + 2.3f;
                while (Time.time < until) yield return null;
                Check(tank.CurrentHealth == health, "Stationary boss hit out of melee range.");
                Warp(tank, boss.transform.position + Vector3.back * 1f);
                deadline = Time.time + 2.5f;
                while (tank.CurrentHealth == health) { Check(Time.time < deadline, "Boss did not attack in melee range."); yield return null; }
                count = events.Count;
                until = Time.time + 1f;
                while (Time.time < until) yield return null;
                Check(events.Count == count, "Boss ignored its periodic interval.");
                combat.ApplyDamage(null, tank, int.MaxValue);
                count = events.Count;
                until = Time.time + 2.3f;
                while (Time.time < until) yield return null;
                Check(events.Count == count && bossAttack.State == "Idle", "Boss continued attacking a dead Tank.");
                Check(Vector3.Distance(boss.transform.position, original[4]) < 0.01f, "Boss moved.");

                Reset();
                Warp(tank, new Vector3(-8f, 0f, -10f));
                Warp(melee, new Vector3(-7f, 0f, -10f));
                Warp(healer, new Vector3(-9f, 0f, -10f));
                combat.ApplyDamage(null, tank, 10);
                combat.ApplyDamage(null, melee, 90);
                combat.ApplyDamage(null, healer, 50);
                brains[1].enabled = true;
                deadline = Time.time + 14f;
                while (tank.CurrentHealth < tank.MaximumHealth || melee.CurrentHealth < melee.MaximumHealth || healer.CurrentHealth < healer.MaximumHealth)
                { Check(Time.time < deadline, "Healer priority/ally/self healing did not finish. " + Describe()); yield return null; }
                Check(events.First(e => e.Healing > 0).Target == tank, "Tank healing priority was not respected.");
                Check(events.Any(e => e.Source == healer && e.Target == healer && e.Healing > 0), "Self-healing did not use the combat pipeline.");
                combat.ApplyDamage(null, melee, int.MaxValue);
                until = Time.time + 2f;
                while (Time.time < until) yield return null;
                Check(!melee.IsAlive, "Healer revived a dead ally.");
                Debug.Log("PARTY AI CHECK: boss cadence, healing priority/self and damage floor passed.");

                Reset();
                combat.KeepBossAlive = true;
                Warp(tank, boss.transform.position + Vector3.back * 1f);
                Warp(ranged, boss.transform.position + Vector3.back * 12f);
                var mover = tank.GetComponent<UnitMovement>();
                Check(mover.TryMove(new Vector3(-8f, 0f, -5f), MovementSource.Manual) == MoveResult.Accepted, "Manual order failed.");
                int command = mover.CommandId;
                health = boss.CurrentHealth;
                brains[0].enabled = brains[3].enabled = true;
                var selection = Camera.main.GetComponent<UnitSelectionController>();
                selection.Select(healer.GetComponent<SelectableUnit>());
                deadline = Time.time + 1f;
                while (!events.Any(e => e.Source == tank && e.Damage > 0))
                { Check(Time.time < deadline, "Tank did not attack during manual movement."); yield return null; }
                Check(mover.HasManualOverride && mover.CommandId == command, "AI replaced the manual command.");
                Check(mover.TryMove(new Vector3(100f, 0f, 100f), MovementSource.Manual) != MoveResult.Accepted && mover.CommandId == command,
                    "Rejected order changed ownership.");
                deadline = Time.time + 10f;
                while (mover.HasManualOverride) { Check(Time.time < deadline, "Manual order never released ownership."); yield return null; }
                Check(events.Any(e => e.Source == ranged && e.Damage > 0), "Other unit stopped when selection/manual order changed.");
                deadline = Time.time + 12f;
                while (CombatPositionSearch.PlanarDistance(tank.transform.position, boss.transform.position) > tank.MeleeRange)
                { Check(Time.time < deadline, "AI did not resume after manual completion. " + Describe()); yield return null; }
                Check(mover.Source == MovementSource.Autonomous, "Resume did not use autonomous command source.");
                Check(mover.TryMove(new Vector3(-8f, 0f, -5f), MovementSource.Manual) == MoveResult.Accepted, "Second manual order failed.");
                mover.Cancel();
                until = Time.time + 0.4f;
                while (Time.time < until) yield return null;
                Check(!mover.HasManualOverride && brains[0].State != RoleState.ManualMove, "Cancellation failed to resume AI.");

                // Put the existing west column between a ranged unit/healer/melee unit and its target.
                foreach (CombatUnit actor in new[] { ranged, healer, tank })
                {
                    Reset();
                    combat.KeepBossAlive = true;
                    boss.transform.position = new Vector3(-5f, original[4].y, 7f);
                    Warp(tank, new Vector3(-5f, 0f, 7f));
                    Warp(actor, new Vector3(-5f, 0f, -1f));
                    CombatUnit target = actor == healer ? tank : boss;
                    if (actor == healer) combat.ApplyDamage(null, tank, 70);
                    Check(!combat.HasLineOfSight(actor.transform.position, target.transform.position), "Sight fixture is not blocked by west column.");
                    actor.GetComponent<PartyRoleBrain>().enabled = true;
                    deadline = Time.time + 18f;
                    while (!events.Any(e => e.Source == actor && (e.Damage > 0 || e.Healing > 0)))
                    { Check(Time.time < deadline, actor.name + " failed to path around blocked sight. " + Describe()); yield return null; }
                    Check(combat.HasLineOfSight(actor.transform.position, target.transform.position), actor.name + " acted through obstacle.");
                }
                Debug.Log("PARTY AI CHECK: manual priority/resume, independent units and obstacle sight routing passed.");

                Reset();
                combat.KeepBossAlive = true;
                Warp(ranged, boss.transform.position + Vector3.back * 2f);
                brains[3].enabled = true;
                deadline = Time.time + 12f;
                while (CombatPositionSearch.PlanarDistance(ranged.transform.position, boss.transform.position) < 10f)
                { Check(Time.time < deadline, "Ranged DPS did not restore its preferred spacing."); yield return null; }
                Check(CombatPositionSearch.PlanarDistance(ranged.transform.position, boss.transform.position) <= ranged.RangedRange,
                    "Ranged spacing moved outside attack range.");
                Reset();
                boss.transform.position = new Vector3(60f, 1.5f, 60f);
                brains[3].enabled = true;
                until = Time.time + 1.2f;
                while (Time.time < until) yield return null;
                Check(brains[3].State == RoleState.NoReachablePosition && !ranged.GetComponent<UnitMovement>().HasManualOverride
                    && events.Count == 0, "Unreachable target did not fail without taking manual ownership.");

                Reset();
                combat.KeepBossAlive = true;
                foreach (PartyRoleBrain brain in brains) brain.enabled = true;
                bossAttack.enabled = true;
                until = Time.time + 3f;
                while (Time.time < until) yield return null;
                combat.ApplyDamage(null, ranged, int.MaxValue);
                until = Time.time + 0.4f;
                while (Time.time < until) yield return null;
                Check(brains[3].State == RoleState.Dead && !ranged.GetComponent<UnitMovement>().enabled, "Dead unit retained autonomous behavior.");
                combat.ResetSession(units);
                events.Clear();
                deadline = Time.time + 12f;
                while (!events.Any(e => e.Source == ranged && e.Damage > 0 && e.Target == boss))
                { Check(Time.time < deadline, "Reset did not restore AI combat."); yield return null; }
            }
            finally
            {
                SuspendAutonomy();
                combat.KeepBossAlive = false;
                combat.EventResolved -= record;
                combat.ResetSession(units);
                for (int i = 0; i < units.Length; i++)
                    if (i < 4) Warp(units[i], original[i]); else units[i].transform.position = original[i];
            }

            void Reset()
            {
                SuspendAutonomy();
                combat.KeepBossAlive = false;
                combat.ResetSession(units);
                for (int i = 0; i < units.Length; i++)
                    if (i < 4) Warp(units[i], original[i]); else units[i].transform.position = original[i];
                events.Clear();
            }
            string Describe() => string.Join("; ", brains.Select(b => b.name + "=" + b.State + "@" + b.transform.position));
        }

        private static void Warp(CombatUnit unit, Vector3 position)
        {
            var mover = unit.GetComponent<UnitMovement>();
            mover.Cancel();
            var agent = unit.GetComponent<NavMeshAgent>();
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            Check(NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, filter), "Fixture is off NavMesh: " + position);
            Check(agent.Warp(hit.position + Vector3.up * agent.baseOffset), "Could not warp " + unit.name);
            agent.ResetPath();
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
