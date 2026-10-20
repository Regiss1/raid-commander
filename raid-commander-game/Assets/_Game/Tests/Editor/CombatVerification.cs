using System;
using System.Collections;
using RaidCommander.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RaidCommander.Verification
{
    /// <summary>Exercises combat calculations and encounter logging in the saved arena.</summary>
    [InitializeOnLoad]
    public static class CombatVerification
    {
        private const string PendingKey = "RaidCommander.CombatVerification.Pending";
        private static IEnumerator checks;
        private static double startedAt;

        static CombatVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
                SessionState.SetBool(PendingKey, false);
                startedAt = EditorApplication.timeSinceStartup;
                checks = Verify();
                EditorApplication.update += Tick;
            };
        }

        [MenuItem("Raid Commander/Verify Combat in Play Mode")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run combat verification from Edit mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/RaidArena.unity");
            SessionState.SetBool(PendingKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup - startedAt < 30, "Verification timed out.");
                EditorApplication.QueuePlayerLoopUpdate();
                if (!checks.MoveNext()) Finish(true, "all combat checks passed");
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        private static void Finish(bool passed, string message)
        {
            EditorApplication.update -= Tick;
            (checks as IDisposable)?.Dispose();
            checks = null;
            Debug.Log((passed ? "COMBAT VERIFY PASS: " : "COMBAT VERIFY FAIL: ") + message);
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }

        private static IEnumerator Verify()
        {
            yield return null;
            yield return null;
            CombatUnit tank = GameObject.Find("Tank").GetComponent<CombatUnit>();
            CombatUnit healer = GameObject.Find("Healer").GetComponent<CombatUnit>();
            CombatUnit dps = GameObject.Find("DPS").GetComponent<CombatUnit>();
            CombatUnit boss = GameObject.Find("Raid Boss").GetComponent<CombatUnit>();
            CombatSystem combat = UnityEngine.Object.FindFirstObjectByType<CombatSystem>();
            CombatLogService log = combat.LogService;
            combat.ResetSession(new[] { tank, healer, dps, boss });

            Vector3 bossPosition = boss.transform.position;
            boss.transform.position = tank.transform.position + Vector3.forward * 4f;
            Check(!combat.TryMeleeAttack(tank, boss) && log.History.Count == 0,
                "Out-of-range melee request was accepted or logged.");
            boss.transform.position = tank.transform.position + new Vector3(0f, 10f, 2f);
            Check(combat.TryMeleeAttack(tank, boss), "Melee attack within XZ range failed.");
            Check(boss.CurrentHealth == boss.MaximumHealth - tank.BasicDamage, "Melee damage was not applied.");
            Check(log.History.Count == 1 && log.History[0].Type == CombatEventType.SwingDamage
                && log.History[0].Damage == tank.BasicDamage && log.History[0].SourceId == tank.EncounterId
                && log.History[0].TargetId == boss.EncounterId, "Resolved melee entry is incorrect.");
            combat.ApplyDamage(null, tank, 30);
            Check(combat.TryHeal(healer, tank), "Basic spell heal failed.");
            Check(log.History[2].Type == CombatEventType.SpellHeal && log.History[2].Healing == healer.BasicHealing,
                "Effective healing was not logged.");
            Check(log.History[2].Sequence == log.History[1].Sequence + 1
                && log.History[2].Timestamp >= log.History[1].Timestamp, "Sequence or timestamp ordering is invalid.");

            combat.ApplyDamage(null, tank, 30);
            combat.ApplyHealing(healer, tank, tank.MaximumHealth);
            Check(tank.CurrentHealth == tank.MaximumHealth, "Healing did not cap at maximum health.");

            int deathEvents = 0;
            foreach (CombatLogEntry entry in log.History)
                if (entry.Type == CombatEventType.UnitDied && entry.TargetId == boss.EncounterId) deathEvents++;
            combat.ApplyDamage(dps, boss, boss.CurrentHealth);
            Check(!boss.IsAlive, "Lethal damage did not kill the target.");
            Check(log.History[log.History.Count - 2].Type == CombatEventType.SwingDamage
                && log.History[log.History.Count - 1].Type == CombatEventType.UnitDied,
                "Death must follow its lethal damage entry.");
            Check(combat.ApplyDamage(dps, boss, 10) == 0, "Dead unit accepted more damage.");
            Check(log.History[log.History.Count - 1].Type == CombatEventType.UnitDied,
                "Repeated damage published a duplicate death event.");
            Check(deathEvents == 0, "Unexpected boss death before lethal hit.");

            var serializedLog = new SerializedObject(log);
            serializedLog.FindProperty("historyLimit").intValue = 3;
            serializedLog.ApplyModifiedPropertiesWithoutUndo();
            log.ResetSession();
            combat.TryMeleeAttack(tank, boss); // Dead source must not act.
            combat.ResetSession(new[] { tank, healer, dps, boss });
            boss.transform.position = tank.transform.position + Vector3.forward * 2f;
            for (int i = 0; i < 5; i++) combat.TryMeleeAttack(tank, boss);
            Check(log.History.Count == 3 && log.History[0].Sequence == 3,
                "Bounded history did not discard the oldest entries.");
            Check(log.History[0].Timestamp >= 0d, "Session timestamps must be nonnegative.");
            combat.ApplyDamage(null, dps, dps.CurrentHealth);
            Check(!dps.GetComponent<RaidCommander.Movement.UnitMovement>().enabled,
                "A dead party member retained its movement executor.");
            log.ResetSession();
            combat.ResetSession(new[] { tank, healer, dps, boss });
            var combatSettings = new SerializedObject(combat);
            combatSettings.FindProperty("logService").objectReferenceValue = null;
            combatSettings.ApplyModifiedPropertiesWithoutUndo();
            int healthBeforeUnloggedHit = tank.CurrentHealth;
            combat.ApplyDamage(null, tank, 5);
            Check(tank.CurrentHealth == healthBeforeUnloggedHit - 5,
                "Combat failed when no log subscriber was attached.");
            combatSettings.FindProperty("logService").objectReferenceValue = log;
            combatSettings.ApplyModifiedPropertiesWithoutUndo();
            combat.ResetSession(new[] { tank, healer, dps, boss });
            Check(log.History.Count == 0, "Reset did not clear history.");
            boss.transform.position = bossPosition;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
