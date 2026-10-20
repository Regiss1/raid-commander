using System;
using System.Collections;
using System.Linq;
using RaidCommander.Combat;
using RaidCommander.Movement;
using RaidCommander.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace RaidCommander.Verification
{
    /// <summary>Exercises targeting against real 3D obstacle colliders in the saved arena.</summary>
    public static class LineOfSightVerification
    {
        [MenuItem("Raid Commander/Verify Combat Line of Sight in Play Mode")]
        public static void Begin() => CombatVerification.Begin();

        public static IEnumerator Verify()
        {
            var combat = UnityEngine.Object.FindAnyObjectByType<CombatSystem>();
            var actions = UnityEngine.Object.FindAnyObjectByType<CombatDebugActions>();
            var root = GameObject.Find("Obstacles").transform;
            var settings = new SerializedObject(combat);
            Check(settings.FindProperty("obstacleRoot").objectReferenceValue == root, "Saved obstacle root is not wired.");
            var ranged = GameObject.Find("Ranged DPS").GetComponent<CombatUnit>();
            var healer = GameObject.Find("Healer").GetComponent<CombatUnit>();
            var tank = GameObject.Find("Tank").GetComponent<CombatUnit>();
            var boss = GameObject.Find("Raid Boss").GetComponent<CombatUnit>();
            CombatUnit[] units = { ranged, healer, tank, boss };
            Vector3[] positions = units.Select(u => u.transform.position).ToArray();
            Quaternion[] rotations = units.Select(u => u.transform.rotation).ToArray();
            NavMeshAgent[] agents = units.Select(u => u.GetComponent<NavMeshAgent>()).ToArray();
            bool[] agentEnabled = agents.Select(a => a != null && a.enabled).ToArray();
            UnitMovement[] movers = units.Select(u => u.GetComponent<UnitMovement>()).ToArray();
            bool[] moverEnabled = movers.Select(m => m != null && m.enabled).ToArray();
            var line = ranged.GetComponent<LineRenderer>();
            var tankBar = tank.GetComponentInChildren<HealthBarBinding>();
            var bossBar = GameObject.Find("Boss Status HUD").GetComponent<HealthBarBinding>();
            GameObject fixture = null;
            GameObject outside = null;
            int outcomes = 0;
            Action<ResolvedCombatEvent> observer = _ => outcomes++;
            combat.EventResolved += observer;
            try
            {
                for (int i = 0; i < units.Length; i++)
                {
                    if (movers[i] != null) movers[i].enabled = false;
                    if (agents[i] != null) agents[i].enabled = false;
                }
                ranged.transform.position = healer.transform.position = new Vector3(-6f, 1f, -11f);
                boss.transform.position = tank.transform.position = new Vector3(0f, 1f, -11f);
                fixture = new GameObject("LOS verification nested obstacles");
                fixture.transform.SetParent(root, false);
                var blockerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blockerObject.name = "LOS verification blocker";
                blockerObject.transform.SetParent(fixture.transform, false);
                var blocker = blockerObject.GetComponent<BoxCollider>();
                outside = new GameObject("LOS verification outside hierarchy");

                foreach (bool healing in new[] { false, true })
                {
                    blockerObject.transform.SetParent(fixture.transform, true);
                    blockerObject.SetActive(true);
                    blocker.enabled = true;
                    blocker.isTrigger = false;
                    blockerObject.transform.SetPositionAndRotation(new Vector3(-3f, 0.02f, -11f), Quaternion.identity);
                    blockerObject.transform.localScale = new Vector3(1f, 0.04f, 2f);
                    Prepare();
                    Physics.SyncTransforms();
                    Check(!blocker.Raycast(new Ray(new Vector3(-6f, 1f, -11f), Vector3.right), out _, 6f),
                        "Low-cover fixture must lie below a unit-center 3D ray.");
                    AssertBlocked(healing, "low cover");
                    blockerObject.transform.position = new Vector3(-3f, 4f, -11f);
                    blockerObject.transform.localScale = new Vector3(1f, 8f, 2f);
                    Prepare();
                    AssertBlocked(healing, "high cover");
                    blockerObject.transform.position = new Vector3(-3f, 50f, -11f);
                    Prepare();
                    AssertBlocked(healing, "vertically separated obstacle");

                    blocker.enabled = false;
                    AssertClear(healing, "disabled collider");
                    blocker.enabled = true;
                    blocker.isTrigger = true;
                    AssertClear(healing, "trigger collider");
                    blocker.isTrigger = false;
                    fixture.SetActive(false);
                    AssertClear(healing, "inactive obstacle ancestor");
                    fixture.SetActive(true);
                    blockerObject.transform.SetParent(outside.transform, true);
                    AssertClear(healing, "unrelated Default-layer collider");
                    blockerObject.transform.SetParent(fixture.transform, true);
                    blockerObject.transform.position = new Vector3(-8f, 1f, -11f);
                    blockerObject.transform.localScale = Vector3.one;
                    AssertClear(healing, "obstacle behind source");
                    blockerObject.transform.position = new Vector3(2f, 1f, -11f);
                    AssertClear(healing, "obstacle beyond target");
                    blockerObject.transform.position = new Vector3(-3f, 1f, -8f);
                    AssertClear(healing, "clear path after lateral repositioning");

                    // A rotated slender collider's AABB crosses the path but its actual footprint does not.
                    blockerObject.transform.position = new Vector3(-7.2f, 1f, -10.2f);
                    blockerObject.transform.localScale = new Vector3(4f, 2f, 0.1f);
                    blockerObject.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                    AssertClear(healing, "rotated shape instead of its AABB");

                    blockerObject.transform.SetPositionAndRotation(new Vector3(-3f, 1f, -11f), Quaternion.identity);
                    blockerObject.transform.localScale = Vector3.one;
                    Prepare();
                    CombatUnit source = healing ? healer : ranged;
                    CombatUnit target = healing ? tank : boss;
                    target.transform.position = source.transform.position + Vector3.right * 19f;
                    if (healing) actions.HealerHeal(); else actions.RangedDpsAttack();
                    Check(actions.LastStatus.Contains("Move closer"), "Sight failure hid existing range feedback.");
                    target.transform.position = new Vector3(0f, 1f, -11f);
                    combat.ApplyDamage(null, source, source.CurrentHealth);
                    if (healing) actions.HealerHeal(); else actions.RangedDpsAttack();
                    Check(actions.LastStatus.Contains("defeated"), "Sight failure hid dead-source feedback.");
                    Prepare();
                    combat.ApplyDamage(null, target, target.CurrentHealth);
                    if (healing) actions.HealerHeal(); else actions.RangedDpsAttack();
                    Check(actions.LastStatus.Contains("defeated"), "Sight failure hid dead-target feedback.");

                    // Initial overlap must not bypass obstruction, including a zero-length XZ segment.
                    Prepare();
                    blockerObject.transform.position = source.transform.position;
                    AssertBlocked(healing, "source inside obstacle footprint");
                    target.transform.position = source.transform.position + Vector3.up * 20f;
                    Prepare();
                    AssertBlocked(healing, "overlapping source/target footprints");
                    target.transform.position = new Vector3(0f, 1f, -11f);
                }

                // Unit bodies/ground outside Obstacles are passable to sight despite intersecting the query.
                blocker.enabled = false;
                AssertClear(false, "unit bodies and ground");
                AssertClear(true, "unit bodies and ground");

                // Exercise the arena's existing non-box column collider, not only synthetic boxes.
                foreach (bool healing in new[] { false, true })
                {
                    ranged.transform.position = healer.transform.position = new Vector3(3f, 1f, 5f);
                    boss.transform.position = tank.transform.position = new Vector3(9f, 1f, 5f);
                    Prepare();
                    AssertBlocked(healing, "existing arena column");
                    ranged.transform.position = healer.transform.position = new Vector3(3f, 1f, 6.5f);
                    boss.transform.position = tank.transform.position = new Vector3(9f, 1f, 6.5f);
                    AssertClear(healing, "path beside existing arena column");
                }
                ranged.transform.position = healer.transform.position = new Vector3(-6f, 1f, -11f);
                boss.transform.position = tank.transform.position = new Vector3(0f, 1f, -11f);

                // Trusted resolution and melee are outside the new targeting restriction.
                Prepare();
                blocker.enabled = true;
                blockerObject.transform.position = new Vector3(-0.8f, 1f, -11f);
                tank.transform.position = new Vector3(-1.5f, 1f, -11f);
                Check(combat.TryMeleeAttack(tank, boss), "Obstacle incorrectly blocked melee.");
                int before = boss.CurrentHealth;
                actions.DamageTank();
                Check(tank.CurrentHealth < tank.MaximumHealth, "Direct debug damage failed behind cover.");
                Check(combat.ApplyDamage(ranged, boss, 3) == 3 && boss.CurrentHealth == before - 3,
                    "Trusted direct damage acquired a sight check.");
                Check(combat.ApplyHealing(healer, tank, 3) == 3, "Trusted direct healing acquired a sight check.");
                actions.KillBoss();
                Check(!boss.IsAlive, "Direct debug kill acquired a sight check.");
                actions.ResetEncounter();
                Check(boss.IsAlive && tank.CurrentHealth == tank.MaximumHealth, "Debug reset regressed.");
                int frame = Time.frameCount;
                while (Time.frameCount < frame + 2) yield return null;
                Debug.Log("LINE OF SIGHT VERIFY PASS: ranged/heal low/high/elevated cover, hierarchy/activity/trigger filtering, segment endpoints, true collider shapes, initial overlap, debug-only failure, unchanged health/events, clear recovery and melee/direct-action regressions.");

                void Prepare()
                {
                    actions.ResetEncounter();
                    combat.ApplyDamage(null, tank, 40);
                    combat.LogService.ResetSession();
                    line.enabled = false;
                }

                void AssertBlocked(bool healing, string context)
                {
                    int health = healing ? tank.CurrentHealth : boss.CurrentHealth;
                    float fill = healing ? tankBar.FillImage.fillAmount : bossBar.FillImage.fillAmount;
                    int observed = outcomes;
                    bool result = healing ? combat.TryHeal(healer, tank, out CombatActionFailure failure)
                        : combat.TryRangedAttack(ranged, boss, out failure);
                    Check(!result && failure == CombatActionFailure.LineOfSight, "Expected sight rejection: " + context);
                    if (healing) actions.HealerHeal(); else actions.RangedDpsAttack();
                    Check(actions.LastStatus == (healing ? "Healer" : "Ranged DPS") + " failed because of line of sight",
                        "Incorrect debug failure message: " + context);
                    Check((healing ? tank.CurrentHealth : boss.CurrentHealth) == health
                        && (healing ? tankBar.FillImage.fillAmount : bossBar.FillImage.fillAmount) == fill
                        && combat.LogService.History.Count == 0 && outcomes == observed && !line.enabled,
                        "Blocked action changed health/presentation/events/history: " + context);
                }

                void AssertClear(bool healing, string context)
                {
                    Prepare();
                    int health = healing ? tank.CurrentHealth : boss.CurrentHealth;
                    if (healing) actions.HealerHeal(); else actions.RangedDpsAttack();
                    Check((healing ? tank.CurrentHealth - health : health - boss.CurrentHealth)
                        == (healing ? healer.BasicHealing : ranged.BasicDamage), "Clear action did not resolve: " + context);
                    Check(combat.LogService.History.Count == 1 && combat.LogService.History[0].Type
                        == (healing ? CombatEventType.SpellHeal : CombatEventType.RangedDamage), "Clear action log regressed: " + context);
                    Check(healing || line.enabled, "Clear ranged hit lost its tracer: " + context);
                }
            }
            finally
            {
                combat.EventResolved -= observer;
                if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
                if (outside != null) UnityEngine.Object.DestroyImmediate(outside);
                actions.ResetEncounter();
                line.enabled = false;
                for (int i = 0; i < units.Length; i++)
                {
                    if (movers[i] != null) movers[i].Cancel();
                    units[i].transform.SetPositionAndRotation(positions[i], rotations[i]);
                    if (agents[i] != null)
                    {
                        agents[i].enabled = agentEnabled[i];
                        if (agentEnabled[i]) agents[i].Warp(positions[i]);
                    }
                    if (movers[i] != null) movers[i].enabled = moverEnabled[i];
                }
                Physics.SyncTransforms();
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
