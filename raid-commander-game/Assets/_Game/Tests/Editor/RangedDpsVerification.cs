using System;
using System.Collections;
using System.Linq;
using RaidCommander.Combat;
using RaidCommander.Movement;
using RaidCommander.Selection;
using RaidCommander.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RaidCommander.Verification
{
    /// <summary>Exercises the actual saved fourth unit, input actions, debug producer and combat observers.</summary>
    public static class RangedDpsVerification
    {
        [MenuItem("Raid Commander/Verify Ranged DPS in Play Mode")]
        public static void Begin() => CombatVerification.Begin();

        public static IEnumerator Verify()
        {
            var ranged = GameObject.Find("Ranged DPS").GetComponent<CombatUnit>();
            var tank = GameObject.Find("Tank").GetComponent<CombatUnit>();
            var healer = GameObject.Find("Healer").GetComponent<CombatUnit>();
            var melee = GameObject.Find("DPS").GetComponent<CombatUnit>();
            var boss = GameObject.Find("Raid Boss").GetComponent<CombatUnit>();
            var combat = UnityEngine.Object.FindAnyObjectByType<CombatSystem>();
            var actions = UnityEngine.Object.FindAnyObjectByType<CombatDebugActions>();
            var log = combat.LogService;
            var bar = ranged.GetComponentInChildren<HealthBarBinding>();
            var bossBar = GameObject.Find("Boss Status HUD").GetComponent<HealthBarBinding>();
            var tracer = ranged.GetComponent<RangedAttackTracer>();
            var line = ranged.GetComponent<LineRenderer>();
            var movement = ranged.GetComponent<UnitMovement>();
            var agent = ranged.GetComponent<NavMeshAgent>();
            var camera = Camera.main;
            var selection = camera.GetComponent<UnitSelectionController>();
            var selectedBefore = selection.SelectedUnit;
            var ring = UnityEngine.Object.FindAnyObjectByType<SelectionRing>();
            var bossPosition = boss.transform.position;
            var rangedPosition = ranged.transform.position;
            var background = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            var logSettings = new SerializedObject(log);
            int logLimit = logSettings.FindProperty("historyLimit").intValue;
            Keyboard keyboard = null;
            Mouse mouse = null;
            int resolved = 0;
            Action<ResolvedCombatEvent> count = outcome => { if (outcome.Type == CombatEventType.RangedDamage) resolved++; };
            combat.EventResolved += count;
            try
            {
                logSettings.FindProperty("historyLimit").intValue = 1000;
                logSettings.ApplyModifiedPropertiesWithoutUndo();
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>();
                mouse = InputSystem.AddDevice<Mouse>();
                actions.ResetEncounter();
                Check(UnityEngine.Object.FindObjectsByType<SelectableUnit>().Length == 4, "Expected four selectable party units.");
                Check(UnityEngine.Object.FindObjectsByType<CombatUnit>().Select(u => u.EncounterId).Distinct().Count() == 5,
                    "Encounter IDs are not unique across all five units.");
                Check(ranged.Faction == CombatFaction.Friendly && ranged.GetComponent<SelectableUnit>().Role == PartyRole.RangedDps,
                    "Ranged DPS faction or role is incorrect.");
                Check(ranged.MaximumHealth == 100 && ranged.BasicDamage == 20 && ranged.RangedRange == 18f
                    && agent.speed == 4.5f && agent.isOnNavMesh, "Ranged DPS defaults/navigation are incorrect.");
                Check(agent.obstacleAvoidanceType == ObstacleAvoidanceType.NoObstacleAvoidance,
                    "Ranged DPS no longer allows pass-through movement.");
                Check(bar != null && bar.Unit == ranged && bar.FillImage.fillAmount == 1f, "Fourth reactive bar is not wired.");
                Color bodyColor = ranged.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
                Check(bodyColor.g > 0.8f && bodyColor.b > 0.8f && bodyColor.r < 0.1f, "Ranged placeholder is not cyan.");

                CombatUnit[] party = { tank, healer, melee, ranged };
                Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
                for (int i = 0; i < keys.Length; i++)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys[i]));
                    int frame = Time.frameCount;
                    while (Time.frameCount < frame + 2) yield return null;
                    Check(selection.SelectedUnit == party[i].GetComponent<SelectableUnit>(), "Selection hotkey failed: " + keys[i]);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    frame = Time.frameCount;
                    while (Time.frameCount < frame + 2) yield return null;
                }
                Check(ring.GetComponent<MeshRenderer>().enabled && Mathf.Abs(ring.transform.position.x - ranged.transform.position.x) < 0.01f,
                    "Selection ring did not follow the fourth unit.");
                selection.Select(null);
                Physics.SyncTransforms();
                Vector2 screen = camera.WorldToScreenPoint(ranged.transform.position);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Left));
                int inputFrame = Time.frameCount;
                while (Time.frameCount < inputFrame + 2) yield return null;
                Check(selection.SelectedUnit == ranged.GetComponent<SelectableUnit>(), "Mouse selection of Ranged DPS failed.");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                inputFrame = Time.frameCount;
                while (Time.frameCount < inputFrame + 2) yield return null;
                Vector3 destination = new Vector3(-8f, 0f, -7f);
                screen = camera.WorldToScreenPoint(destination);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Right));
                inputFrame = Time.frameCount;
                while (Time.frameCount < inputFrame + 2) yield return null;
                Check(movement.HasManualOverride && Vector3.Distance(movement.Destination, destination) < 0.2f,
                    "Ranged DPS right-click did not acquire its own manual movement.");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                selection.Select(tank.GetComponent<SelectableUnit>());
                Check(movement.HasManualOverride && movement.TryMove(Vector3.zero, MovementSource.Autonomous) == MoveResult.ManualOverrideActive,
                    "Selection/autonomous input interrupted the ranged unit's manual order.");
                double deadline = EditorApplication.timeSinceStartup + 8;
                while (movement.State == MovementState.Moving)
                {
                    Check(EditorApplication.timeSinceStartup < deadline, "Ranged DPS movement did not finish.");
                    yield return null;
                }
                Check(movement.State == MovementState.Completed && !movement.HasManualOverride, "Ranged movement ownership did not end.");
                Check(Vector3.Distance(bar.transform.position, ranged.transform.position + Vector3.up * 1.8f) < 0.03f,
                    "Ranged health bar did not follow movement.");

                // Exact boundary, XZ elevation independence, facing, immediate health and log metadata.
                boss.transform.position = ranged.transform.position + new Vector3(0f, 10f, ranged.RangedRange);
                int before = boss.CurrentHealth;
                actions.RangedDpsAttack();
                Check(boss.CurrentHealth == before - ranged.BasicDamage && resolved == 1, "Ranged debug attack did not resolve immediately once.");
                Check(ranged.CurrentTarget == boss && Vector3.Dot(ranged.transform.forward, Vector3.forward) > 0.999f,
                    "Ranged attacker did not face/store its target on XZ.");
                CombatLogEntry entry = log.History.Last();
                Check(entry.Type == CombatEventType.RangedDamage && entry.SourceId == ranged.EncounterId
                    && entry.TargetId == boss.EncounterId && entry.Damage == ranged.BasicDamage && entry.SpellId == null
                    && entry.Flags == CombatLogFlags.None && entry.Sequence == 1 && entry.Timestamp >= 0,
                    "Ranged log metadata is incorrect.");
                Check(Mathf.Approximately(bossBar.FillImage.fillAmount, (float)boss.CurrentHealth / boss.MaximumHealth),
                    $"Boss HUD did not respond to ranged damage: fill={bossBar.FillImage.fillAmount}, health={boss.CurrentHealth}/{boss.MaximumHealth}.");
                Check(line.enabled, $"Tracer did not respond to ranged damage: active={tracer.isActiveAndEnabled}.");
                Check(Vector3.Distance(line.GetPosition(0), ranged.transform.position) < 0.01f
                    && Vector3.Distance(line.GetPosition(1), boss.transform.position) < 0.01f, "Tracer endpoints are incorrect.");
                float expires = Time.time + 0.3f;
                while (Time.time < expires) yield return null;
                Check(!line.enabled, "Tracer did not expire.");
                boss.transform.position += Vector3.forward * 0.01f;
                before = boss.CurrentHealth;
                actions.RangedDpsAttack();
                Check(boss.CurrentHealth == before && log.History.Count == 1 && resolved == 1 && !line.enabled
                    && actions.LastStatus.Contains("Move closer"), "Out-of-range attack caused effects or lacked feedback.");
                Check(!combat.TryRangedAttack(ranged, tank) && !combat.TryRangedAttack(ranged, ranged)
                    && !combat.TryRangedAttack(ranged, null) && !combat.TryRangedAttack(null, boss), "Invalid ranged target/source accepted.");
                Check(log.History.Count == 1 && resolved == 1, "Rejected attack published an outcome.");

                boss.transform.position = ranged.transform.position + Vector3.forward * 2f;
                combat.ApplyDamage(null, boss, boss.CurrentHealth - 7);
                log.ResetSession();
                actions.RangedDpsAttack();
                Check(log.History.Count == 2 && log.History[0].Type == CombatEventType.RangedDamage
                    && log.History[0].Damage == 7 && log.History[1].Type == CombatEventType.UnitDied
                    && log.History[1].SourceId == ranged.EncounterId && !boss.IsAlive, "Lethal ranged event sequence/effective damage is incorrect.");
                actions.RangedDpsAttack();
                Check(log.History.Count == 2 && actions.LastStatus.Contains("defeated"), "Dead target accepted another hit or lacked feedback.");
                actions.ResetEncounter();
                Check(movement.TryMove(destination + Vector3.right * 2f, MovementSource.Manual) == MoveResult.Accepted,
                    "Could not start manual movement for death test.");
                combat.ApplyDamage(boss, ranged, ranged.CurrentHealth);
                Check(!movement.enabled && !movement.HasManualOverride && agent.isStopped && bar.FillImage.fillAmount == 0f,
                    "Death did not stop ranged movement/update its bar.");
                int eventCount = log.History.Count;
                actions.RangedDpsAttack();
                Check(log.History.Count == eventCount && actions.LastStatus.Contains("defeated"), "Dead source attacked or lacked feedback.");
                actions.ResetEncounter();
                Check(ranged.IsAlive && movement.enabled && log.History.Count == 0 && bar.FillImage.fillAmount == 1f,
                    "Debug reset omitted Ranged DPS health/movement/log.");
                combat.ApplyDamage(boss, ranged, 30);
                Check(Mathf.Approximately(bar.FillImage.fillAmount, 0.7f), "Ranged bar ignored damage.");
                combat.ApplyHealing(healer, ranged, 10);
                Check(Mathf.Approximately(bar.FillImage.fillAmount, 0.8f), "Ranged bar ignored healing.");

                // Neither logging nor VFX is required to resolve ranged damage.
                var settings = new SerializedObject(combat);
                settings.FindProperty("logService").objectReferenceValue = null;
                settings.ApplyModifiedPropertiesWithoutUndo();
                tracer.enabled = false;
                try
                {
                    before = boss.CurrentHealth;
                    Check(combat.TryRangedAttack(ranged, boss) && boss.CurrentHealth == before - ranged.BasicDamage && !line.enabled,
                        "Ranged damage depends on logging or presentation.");
                }
                finally
                {
                    settings.FindProperty("logService").objectReferenceValue = log;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    tracer.enabled = true;
                }
                actions.ResetEncounter();
                log.ResetSession();
                actions.RangedDpsAttack();
                Check(log.History.Count == 1, "Tracer re-enable duplicated combat subscriptions/outcomes.");
                Debug.Log("RANGED DPS VERIFY PASS: four hotkeys, mouse/ring, right-click movement, manual ownership, stats, immediate range/faction/death checks, effective events, reset, reactive bars and independent tracer.");
            }
            finally
            {
                combat.EventResolved -= count;
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
                movement.Cancel();
                actions.ResetEncounter();
                agent.Warp(rangedPosition);
                boss.transform.position = bossPosition;
                selection.Select(selectedBefore);
                logSettings.FindProperty("historyLimit").intValue = logLimit;
                logSettings.ApplyModifiedPropertiesWithoutUndo();
                tracer.enabled = true;
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
