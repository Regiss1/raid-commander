using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RaidCommander.Abilities;
using RaidCommander.Combat;
using RaidCommander.Encounters;
using RaidCommander.Movement;
using RaidCommander.UI;
using RaidCommander.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace RaidCommander.Verification
{
    [InitializeOnLoad]
    public static class AwarenessVerification
    {
        private const string Pending = "RaidCommander.T10.Verify";
        private static readonly Stack<IEnumerator> checks = new Stack<IEnumerator>();
        private static int frame;
        private static double started;
        private static bool background;
        public static string Result { get; private set; } = "Not run";

        static AwarenessVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
                SessionState.EraseBool(Pending);
                background = Application.runInBackground; Application.runInBackground = true;
                started = EditorApplication.timeSinceStartup; frame = -1; Result = "Running";
                checks.Clear(); checks.Push(Run()); EditorApplication.update += Tick;
            };
        }

        [MenuItem("Raid Commander/Verify Awareness and Decisions in Play Mode")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Start T10 verification from a saved Edit-mode arena.");
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/RaidArena.unity");
            SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup - started < 90, "T10 verification timed out.");
                EditorApplication.QueuePlayerLoopUpdate();
                if (frame == Time.frameCount) return;
                frame = Time.frameCount;
                while (checks.Count > 0)
                {
                    IEnumerator step = checks.Peek();
                    if (!step.MoveNext()) { (step as IDisposable)?.Dispose(); checks.Pop(); continue; }
                    if (step.Current is IEnumerator nested) { checks.Push(nested); continue; }
                    return;
                }
                Finish(true, "deterministic rolls/timing, all roles, escape recovery, manual priority, bounded history, tab scroll/debug and reset passed");
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        private static void Finish(bool passed, string message)
        {
            EditorApplication.update -= Tick;
            while (checks.Count > 0) (checks.Pop() as IDisposable)?.Dispose();
            Time.timeScale = 1f; Application.runInBackground = background;
            Result = (passed ? "PASS: " : "FAIL: ") + message;
            System.IO.File.WriteAllText("Logs/t10-awareness-verification.txt", Result);
            Debug.Log("AWARENESS VERIFY " + Result);
            EditorApplication.isPlaying = false;
        }

        private static IEnumerator Run()
        {
            yield return null;
            Check(Time.frameCount > 1, "Player loop did not advance.");
            PureChecks();
            var tank = GameObject.Find("Tank").GetComponent<CombatUnit>();
            var boss = GameObject.Find("Raid Boss").GetComponent<CombatUnit>();
            var combat = Object.FindAnyObjectByType<CombatSystem>();
            var hazards = Object.FindAnyObjectByType<GroundHazards>();
            var boundary = Object.FindAnyObjectByType<ArenaBoundary>();
            var decisions = Object.FindAnyObjectByType<DecisionLogService>();
            var hud = Object.FindAnyObjectByType<CombatHud>();
            var brains = Object.FindObjectsByType<PartyRoleBrain>(FindObjectsSortMode.None);
            Check(decisions != null && hud != null && brains.Length == 4, "Saved T10 scene wiring is incomplete.");
            Check(brains.All(b => b.GetComponent<CombatUnit>().Awareness == 5), "Party Awareness is not 5.");
            var statless = new GameObject("T10 statless fixture").AddComponent<CombatUnit>();
            try { Check(statless.Awareness == 5, "Missing-stats Awareness fallback is not 5."); }
            finally { Object.Destroy(statless.gameObject); }
            var originalStats = new SerializedObject(tank).FindProperty("stats").objectReferenceValue;
            var runtimeStats = Object.Instantiate((CombatStatsDefinition)originalStats);
            runtimeStats.hideFlags = HideFlags.DontSave;
            var tankSettings = new SerializedObject(tank);
            var awarenessSetting = new SerializedObject(runtimeStats).FindProperty("awareness");
            try
            {
                tankSettings.FindProperty("stats").objectReferenceValue = runtimeStats;
                tankSettings.ApplyModifiedPropertiesWithoutUndo();
                awarenessSetting.intValue = 7; awarenessSetting.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                Check(tank.Awareness == 7, "Runtime Awareness did not read current stats.");
                awarenessSetting.intValue = 9; awarenessSetting.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                Check(tank.Awareness == 9, "Runtime Awareness cached stale stats.");
            }
            finally
            {
                tankSettings.FindProperty("stats").objectReferenceValue = originalStats;
                tankSettings.ApplyModifiedPropertiesWithoutUndo();
                Object.Destroy(runtimeStats);
            }
            Check(!hud.DecisionsSelected, "Fresh Play did not default to Combat.");
            foreach (var brain in brains) brain.enabled = false;
            boss.GetComponent<BossAbilityRunner>().enabled = false;
            boss.GetComponent<BossBasicAttack>().enabled = false;
            combat.KeepBossAlive = true;
            combat.ResetSession(brains.Select(b => b.GetComponent<CombatUnit>()).Concat(new[] { boss }).ToArray());
            Warp(boss, new Vector3(0f, 0f, 1f));
            Warp(tank, Vector3.zero);
            var footprint = HazardFootprint.Rectangle(new Vector3(0f, 0f, -2f), Vector3.forward, 4f, 10f, boundary);
            object first = new object(), second = new object();
            hazards.Register(first, footprint, "Fixture Flame", 77);
            hazards.Register(second, footprint, "Fixture Flame", 77);
            string[] overlap = hazards.ContainingIdentities(tank.transform.position);
            Check(overlap.Length == 2 && overlap[0] != overlap[1], "Overlapping casts lost distinct identities.");
            int rolls = 0;
            var brainTank = tank.GetComponent<PartyRoleBrain>();
            brainTank.SetAwarenessRollSourceForVerification(() => ++rolls == 1 ? 10 : 1);
            brainTank.enabled = true;
            yield return Wait(0.32f);
            Check(rolls == 1 && !brainTank.EscapeCommitted, "Failed entry check triggered escape or duplicated overlap rolls.");
            Check(decisions.History.Count(e => e.Kind == DecisionKind.AwarenessCheck) == 1 &&
                decisions.History[0].Roll == 10 && decisions.History[0].Awareness == 5 &&
                decisions.History[0].Hazards.Contains(overlap[0]) && decisions.History[0].Hazards.Contains(overlap[1]),
                "First check metadata or result is incorrect.");
            yield return Wait(0.50f);
            Check(rolls == 2 && decisions.History.Count(e => e.Kind == DecisionKind.AwarenessCheck) == 2,
                "Half-second retry did not produce exactly one check.");
            Check(decisions.History.Any(e => e.Kind == DecisionKind.EscapeStarted) ||
                decisions.History.Any(e => e.Kind == DecisionKind.NoSafeExit),
                "Successful detection had no distinct escape outcome.");
            brainTank.enabled = false;
            Warp(tank, Vector3.zero);
            var move = tank.GetComponent<UnitMovement>();
            Check(move.TryMove(new Vector3(0f, 0f, 7f), MovementSource.Manual) == MoveResult.Accepted,
                "Manual fixture order was rejected.");
            brainTank.enabled = true;
            int beforeManual = rolls;
            yield return Wait(0.3f);
            Check(rolls == beforeManual && decisions.History.Any(e => e.Kind == DecisionKind.ManualSuppression),
                "Manual ownership did not suppress and record Awareness.");
            move.Cancel();
            yield return Wait(0.3f);
            Check(rolls > beforeManual, "Manual completion did not restart an immediate check while exposed.");
            brainTank.enabled = false;
            hazards.Remove(first); hazards.Remove(second);
            decisions.ResetSession();
            Warp(tank, Vector3.zero);
            object blocked = new object();
            hazards.Register(blocked, HazardFootprint.Rectangle(new Vector3(0f, 0f, -16.5f),
                Vector3.forward, 100f, 33f, boundary), "Blocked Flame", 88);
            int blockedRolls = 0;
            brainTank.SetAwarenessRollSourceForVerification(() => { blockedRolls++; return 1; });
            brainTank.enabled = true;
            yield return Wait(0.45f);
            Check(decisions.History.Count(e => e.Kind == DecisionKind.NoSafeExit) == 1 && blockedRolls == 1,
                "Blocked escape was not recorded once after detection.");
            yield return Wait(0.55f);
            Check(decisions.History.Count(e => e.Kind == DecisionKind.NoSafeExit) == 1 && blockedRolls == 1,
                "Blocked retries spammed history or required another roll.");
            hazards.Remove(blocked);
            object recovery = new object(); hazards.Register(recovery, footprint, "Recovery Flame", 89);
            yield return Wait(0.45f);
            Check(decisions.History.Any(e => e.Kind == DecisionKind.EscapeStarted) && blockedRolls == 1,
                "Path recovery did not resume the committed escape without another roll.");
            brainTank.enabled = false; hazards.Remove(recovery);

            decisions.ResetSession();
            var members = brains.Select(b => b.GetComponent<CombatUnit>()).ToArray();
            for (int i = 0; i < members.Length; i++) Warp(members[i], new Vector3(-1.5f + i, 0f, 0f));
            object allRoles = new object(); hazards.Register(allRoles, footprint, "All Roles", 90);
            foreach (var brain in brains) { brain.SetAwarenessRollSourceForVerification(() => 1); brain.enabled = true; }
            yield return Wait(0.4f);
            Check(decisions.History.Where(e => e.Kind == DecisionKind.AwarenessCheck && e.Success == true)
                .Select(e => e.MemberId).Distinct().Count() == 4,
                "Not all four roles used the same immediate Awareness check.");
            foreach (var brain in brains) brain.enabled = false;
            hazards.Remove(allRoles);

            int combatBefore = combat.LogService.History.Count;
            decisions.ResetSession();
            Check(combat.LogService.History.Count == combatBefore, "Decision reset cleared combat history.");
            var limit = new SerializedObject(decisions).FindProperty("historyLimit");
            limit.intValue = 3; limit.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            string[] identity = { "Immutable [cast 1]" };
            DecisionLogEntry snapshot = decisions.Record(DecisionKind.AwarenessCheck, tank, identity, 5, 6, false);
            identity[0] = "Mutated";
            for (int i = 0; i < 3; i++) decisions.Record(DecisionKind.EscapeStarted, tank, identity);
            Check(snapshot.Hazards == "Immutable [cast 1]" && decisions.History.Count == 3 &&
                decisions.History[0].Sequence == 2 && decisions.History[2].Sequence == 4,
                "Decision snapshot or 1,000-entry eviction semantics failed at a small fixture limit.");
            limit.intValue = 1000; limit.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            var tabs = hud.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            var decisionTab = tabs.FirstOrDefault(t => t.name == "Decisions Tab");
            var combatTab = tabs.FirstOrDefault(t => t.name == "Combat Tab");
            Check(decisionTab != null && combatTab != null, "Missing Combat/Decisions tabs.");
            decisionTab.onClick.Invoke();
            Check(hud.DecisionsSelected, "Decisions tab did not activate.");
            decisions.ResetSession();
            for (int i = 0; i < 36; i++) decisions.Record(DecisionKind.AwarenessCheck, tank,
                new[] { "Scroll Fixture [cast " + i + "]" }, 5, 6, false);
            for (int i = 0; i < 36; i++) combat.LogService.Record(
                new ResolvedCombatEvent(CombatEventType.SpellCastStart, boss, tank, spellId: 100 + i));
            yield return null;
            var decisionScroll = hud.transform.Find("Combat Log/Decisions Scroll").GetComponent<UnityEngine.UI.ScrollRect>();
            var combatScroll = hud.transform.Find("Combat Log/Scroll").GetComponent<UnityEngine.UI.ScrollRect>();
            typeof(CombatHud).GetField("followDecisions", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hud, false);
            decisionScroll.verticalNormalizedPosition = .7f;
            combatTab.onClick.Invoke();
            typeof(CombatHud).GetField("followLog", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hud, false);
            combatScroll.verticalNormalizedPosition = .4f;
            decisionTab.onClick.Invoke();
            yield return null;
            Check(Mathf.Abs(decisionScroll.verticalNormalizedPosition - .7f) < .06f,
                "Decisions scrollback changed after switching tabs.");
            combatTab.onClick.Invoke();
            yield return null;
            Check(Mathf.Abs(combatScroll.verticalNormalizedPosition - .4f) < .06f,
                "Combat scrollback changed after switching tabs.");
            decisionTab.onClick.Invoke();
            bool originalDebug = SessionState.GetBool("RaidCommander.ShowCombatDebug", false);
            try
            {
                SessionState.SetBool("RaidCommander.ShowCombatDebug", true);
                yield return Wait(0.12f);
                Check(!hud.transform.Find("Combat Log").gameObject.activeSelf &&
                    hud.transform.Find("Combat Debug").gameObject.activeSelf,
                    "Editor debug did not replace the tabs.");
                decisions.Record(DecisionKind.ManualSuppression, tank, new[] { "Hidden [cast 2]" });
                SessionState.SetBool("RaidCommander.ShowCombatDebug", false);
                yield return Wait(0.12f);
                Check(hud.DecisionsSelected && hud.transform.Find("Combat Log").gameObject.activeSelf &&
                    decisions.History.Last().Hazards == "Hidden [cast 2]",
                    "Debug restoration lost the chosen tab or hidden decision entry.");
            }
            finally { SessionState.SetBool("RaidCommander.ShowCombatDebug", originalDebug); }
            combatTab.onClick.Invoke();
            Check(!hud.DecisionsSelected, "Combat tab did not activate.");
            combat.ResetSession(brains.Select(b => b.GetComponent<CombatUnit>()).Concat(new[] { boss }).ToArray());
            Check(decisions.History.Count == 0 && combat.LogService.History.Count == 0,
                "Encounter reset did not clear both histories.");
        }

        private static void PureChecks()
        {
            Check(CombatStatsDefinition.ValidAwareness(0) == 1 && CombatStatsDefinition.ValidAwareness(11) == 10,
                "Awareness clamping failed.");
            AwarenessResult CheckRoll(int stat, int value) => new AwarenessReaction(() => value)
                .Evaluate(true, true, false, 0f, stat);
            Check(CheckRoll(1, 1).Kind == AwarenessResultKind.Succeeded &&
                CheckRoll(1, 2).Kind == AwarenessResultKind.Failed &&
                CheckRoll(5, 5).Kind == AwarenessResultKind.Succeeded &&
                CheckRoll(5, 6).Kind == AwarenessResultKind.Failed &&
                CheckRoll(10, 10).Kind == AwarenessResultKind.Succeeded, "Probability boundaries failed.");
            int count = 0;
            var check = new AwarenessReaction(() => { count++; return count < 3 ? 10 : 1; });
            Check(check.Evaluate(true, true, false, 0f, 5).Kind == AwarenessResultKind.Failed, "Entry was not immediate.");
            Check(check.Evaluate(true, true, false, .49f, 5).Kind == AwarenessResultKind.None && count == 1,
                "Check repeated before 0.5 seconds.");
            Check(check.Evaluate(true, true, false, .5f, 5).Kind == AwarenessResultKind.Failed,
                "Check did not repeat at 0.5 seconds.");
            Check(check.Evaluate(true, true, false, 2f, 5).Kind == AwarenessResultKind.Succeeded && count == 3,
                "Delayed frame produced missed or catch-up checks.");
            Check(check.Evaluate(true, true, false, 2f, 5).Kind == AwarenessResultKind.None && count == 3,
                "Committed or paused check repeated.");
            Check(check.Evaluate(false, true, false, 3f, 5).Kind == AwarenessResultKind.None && check.Committed,
                "Clearance margin did not retain commitment.");
            check.Evaluate(false, false, false, 3f, 5);
            Check(!check.Committed, "Leaving danger did not release commitment.");
            Check(check.Evaluate(true, true, true, 4f, 5).Kind == AwarenessResultKind.ManualSuppression &&
                check.Evaluate(true, true, true, 4.2f, 5).Kind == AwarenessResultKind.None,
                "Manual suppression was repeated.");
            Check(check.Evaluate(true, true, false, 4.3f, 5).Kind == AwarenessResultKind.Succeeded,
                "Manual release did not restart entry timing.");
        }

        private static void Warp(CombatUnit unit, Vector3 point)
        {
            var agent = unit.GetComponent<NavMeshAgent>();
            unit.GetComponent<UnitMovement>().Cancel();
            Check(NavMesh.SamplePosition(point, out NavMeshHit hit, 1.5f,
                new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }),
                "Fixture position is off NavMesh.");
            Check(agent.Warp(hit.position + Vector3.up * agent.baseOffset), "Fixture warp failed.");
            agent.ResetPath();
        }
        private static IEnumerator Wait(float seconds) { float end = Time.time + seconds; while (Time.time < end) yield return null; }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
