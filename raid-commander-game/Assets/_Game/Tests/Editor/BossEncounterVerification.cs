using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RaidCommander.Abilities;
using RaidCommander.Combat;
using RaidCommander.Encounters;
using RaidCommander.Movement;
using RaidCommander.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace RaidCommander.Verification
{
    /// <summary>Actual player-loop acceptance fixtures. All tuning is runtime-only and discarded on Play exit.</summary>
    [InitializeOnLoad]
    public static class BossEncounterVerification
    {
        private const string Pending = "RaidCommander.T8.Verify";
        private static readonly Stack<IEnumerator> checks = new Stack<IEnumerator>();
        private static bool background;
        private static int frame;
        private static double started;
        private static string mode;
        private static CombatSystem combat;
        private static CombatUnit[] party, units;
        private static PartyRoleBrain[] brains;
        private static CombatUnit boss;
        private static BossBasicAttack controller;
        private static BossAbilityRunner runner;
        private static GroundHazards hazards;
        private static ArenaBoundary boundary;
        private static BossAbilityDefinition original, definition;
        private static readonly List<(ResolvedCombatEvent hit, float time)> events = new List<(ResolvedCombatEvent, float)>();
        private static readonly List<string> setupWarnings = new List<string>();
        public static string Result { get; private set; } = "Not run";
        static BossEncounterVerification()
        {
            Application.logMessageReceived += (message, trace, type) =>
            {
                if (type == LogType.Warning && trace.Contains("BossAbilityRunner")) setupWarnings.Add(message);
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || string.IsNullOrEmpty(SessionState.GetString(Pending, ""))) return;
                mode = SessionState.GetString(Pending, ""); SessionState.EraseString(Pending);
                background = Application.runInBackground; Application.runInBackground = true;
                started = EditorApplication.timeSinceStartup; Result = "Running " + mode; frame = -1;
                checks.Clear(); checks.Push(Run()); EditorApplication.update += Tick;
            };
        }
        [MenuItem("Raid Commander/Verify Boss Encounter in Play Mode")]
        public static void Begin() => Start("fast");
        [MenuItem("Raid Commander/Verify Boss Production Timing in Play Mode")]
        public static void BeginProduction() => Start("production");
        [MenuItem("Raid Commander/Verify Combat Regression with Original Bar Fixture")]
        public static void BeginCombatRegression() => Start("combat");
        [MenuItem("Raid Commander/Verify Boss Ability Startup and Recovery in Play Mode")]
        public static void BeginStartup() => Start("startup");
        [MenuItem("Raid Commander/Verify Stable Boss Melee Spacing in Play Mode")]
        public static void BeginSpacing() => Start("spacing");
        private static void Start(string requestedMode)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Start verification from a saved Edit-mode arena.");
            setupWarnings.Clear();
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/RaidArena.unity");
            SessionState.SetString(Pending, requestedMode); EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup - started < 360, "Verification timed out.");
                EditorApplication.QueuePlayerLoopUpdate();
                if (frame == Time.frameCount) return; frame = Time.frameCount;
                while (checks.Count > 0)
                {
                    var top = checks.Peek();
                    if (!top.MoveNext()) { (top as IDisposable)?.Dispose(); checks.Pop(); continue; }
                    if (top.Current is IEnumerator nested) { checks.Push(nested); continue; }
                    return;
                }
                Finish(true, "all " + mode + " acceptance fixtures passed");
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }
        private static void Finish(bool passed, string message)
        {
            EditorApplication.update -= Tick;
            while (checks.Count > 0) (checks.Pop() as IDisposable)?.Dispose();
            Time.timeScale = 1f; Application.runInBackground = background;
            Result = (passed ? "PASS: " : "FAIL: ") + message;
            System.IO.File.WriteAllText("Logs/t8-" + mode + "-verification.txt", Result);
            Debug.Log("BOSS ENCOUNTER VERIFY " + Result); EditorApplication.isPlaying = false;
        }
        private static IEnumerator Run()
        {
            Time.timeScale = mode == "fast" ? 3f : 1f;
            party = new[] { Unit("Tank"), Unit("Healer"), Unit("DPS"), Unit("Ranged DPS") };
            boss = Unit("Raid Boss"); units = party.Concat(new[] { boss }).ToArray();
            brains = party.Select(p => p.GetComponent<PartyRoleBrain>()).ToArray();
            controller = boss.GetComponent<BossBasicAttack>(); runner = boss.GetComponent<BossAbilityRunner>();
            combat = Object.FindAnyObjectByType<CombatSystem>(); hazards = Object.FindAnyObjectByType<GroundHazards>(); boundary = Object.FindAnyObjectByType<ArenaBoundary>();
            original = AssetDatabase.LoadAssetAtPath<BossAbilityDefinition>(EditorTools.BossEncounterSceneSetup.AbilityPath);
            definition = Object.Instantiate(original); definition.hideFlags = HideFlags.DontSave;
            combat.EventResolved += Record;
            try
            {
                Check(original.ValidationError(true) == null && original.CooldownSeconds == 0f
                    && original.Width == 4f && original.Damage == 95,
                    "Saved ability configuration is invalid for the encounter fixtures.");
                Check(controller != null && runner != null && boundary.IsValid, "Missing saved encounter wiring.");
                Check(runner.SetupError == null && setupWarnings.Count == 0, "Fresh Play entry rejected valid saved ability wiring before any fixture reset: " + runner.SetupError);
                if (mode == "startup") { yield return StartupAndRecovery(); yield break; }
                if (mode == "spacing") { yield return SpacingStability(); yield break; }
                if (mode == "production") { yield return Production(); yield break; }
                if (mode == "combat")
                {
                    foreach(var bar in Object.FindObjectsByType<WorldHealthBar>())
                    { var settings=new SerializedObject(bar); settings.FindProperty("screenHeight").floatValue=10f; settings.ApplyModifiedPropertiesWithoutUndo(); }
                    yield return CombatVerification.Verify(); yield break;
                }
                yield return Geometry();
                yield return SpacingStability();
                yield return TargetingAndMovement();
                yield return Abilities();
                yield return LoadoutAndIsolation();
                yield return GroundLifecycle();
                yield return Reactions();
                Debug.Log("T8 CHECK: all fast fixtures completed.");
            }
            finally
            {
                PartyAiVerification.SuspendAutonomy(); combat.EventResolved -= Record;
                SetLoadout(original); combat.KeepBossAlive = false; combat.ResetSession(units);
                Object.Destroy(definition);
            }
        }
        private static void Record(ResolvedCombatEvent hit) => events.Add((hit, Time.time));
        private static IEnumerator StartupAndRecovery()
        {
            // Exercise the actual saved loadout and OnEnable clock before any Reset fixture.
            foreach (var member in units) { member.SetMaximumHealth(20000); combat.ApplyHealing(null, member, 20000); }
            combat.KeepBossAlive = true;
            float activated = PrivateTime("activatedAt");
            int firstFrame = Time.frameCount;
            while (runner.CastCount == 0)
            {
                Check(Time.time - activated < original.InitialDelaySeconds + 0.5f, "Fresh Play entry never recovered/cast on its activation clock.");
                Check(runner.SetupError == null, "Fresh startup validation became invalid.");
                yield return null;
            }
            Check(Time.frameCount > firstFrame && runner.LastCastStartedAt - activated >= original.InitialDelaySeconds - 0.01f,
                "Fresh startup cast began early or without advancing frames.");
            float firstStart = runner.LastCastStartedAt;
            while (runner.IsCasting) yield return null;
            Check(events.Any(e => e.hit.Type == CombatEventType.SpellCastSuccess), "Fresh startup cast did not resolve.");
            combat.ResetSession(units); activated = PrivateTime("activatedAt");
            Check(runner.CastCount == 0 && runner.SetupError == null, "Debug reset retained stale readiness/error.");
            yield return Wait(0.3f);
            Check(runner.CastCount == 0 && Mathf.Abs(PrivateTime("activatedAt") - activated) < 0.001f, "Validation restarted the debug-reset clock.");

            Reset(false); definition.InitialDelaySeconds = 0.8f; definition.IntervalSeconds = 1.1f;
            definition.CastTimeSeconds = 0f; definition.CooldownSeconds = 0f; definition.Damage = 0;
            definition.AllowedTargetRoles = TargetRoles.All;
            SetLoadout(definition); controller.enabled = true; runner.enabled = true;
            activated = PrivateTime("activatedAt");
            var box = boundary.GetComponent<BoxCollider>();
            Vector3 savedSize = box.size;
            try
            {
                setupWarnings.Clear(); box.enabled = false;
                yield return Wait(0.35f);
                Check(runner.SetupError != null && runner.SetupError.Contains("BoxCollider") && runner.CastCount == 0, "Disabled boundary was accepted or had no useful diagnostic.");
                Check(setupWarnings.Count == 1, "Invalid boundary warning repeated every decision.");
                box.enabled = true;
                yield return Wait(0.15f);
                Check(runner.SetupError == null && runner.CastCount == 0, "Boundary repair did not recover before the original deadline.");
                while (runner.CastCount == 0) { Check(Time.time - activated < 1.1f, "Repair restarted the initial-delay clock."); yield return null; }
                float recoveredStart = runner.LastCastStartedAt;
                Check(recoveredStart - activated >= 0.79f, "Repair bypassed the initial delay.");
                definition.Width = 0f; yield return Wait(0.25f);
                Check(runner.SetupError != null && runner.SetupError.Contains("width"), "Invalid width did not identify the dimension failure.");
                definition.Width = 4f; yield return Wait(0.15f);
                Check(runner.SetupError == null && runner.CastCount == 1, "Dimension repair reset/bypassed the interval.");
                while (runner.CastCount == 1) { Check(Time.time - recoveredStart < 1.4f, "Repair restarted interval timing."); yield return null; }
                Check(runner.LastCastStartedAt - recoveredStart >= 1.09f, "Validation bypassed the interval.");

                var settings = new SerializedObject(runner);
                settings.FindProperty("hazards").objectReferenceValue = null; settings.ApplyModifiedPropertiesWithoutUndo();
                yield return Wait(0.15f);
                Check(runner.SetupError != null && runner.SetupError.Contains("hazard"), "Missing hazards were reported as bad dimensions.");
                settings.Update(); settings.FindProperty("hazards").objectReferenceValue = hazards; settings.ApplyModifiedPropertiesWithoutUndo();
                settings.Update(); settings.FindProperty("boundary").objectReferenceValue = null; settings.ApplyModifiedPropertiesWithoutUndo();
                yield return Wait(0.15f);
                Check(runner.SetupError != null && runner.SetupError.Contains("boundary reference"), "Missing boundary reference was not diagnosed.");
                settings.Update(); settings.FindProperty("boundary").objectReferenceValue = boundary; settings.ApplyModifiedPropertiesWithoutUndo();
                box.size = new Vector3(0f, savedSize.y, savedSize.z); yield return Wait(0.15f);
                Check(runner.SetupError != null && !boundary.IsValid, "Degenerate boundary was accepted.");
                box.size = savedSize; yield return Wait(0.15f);
                Check(runner.SetupError == null, "Restored geometry did not recover without a loadout change.");
                combat.ResetSession(units); activated = PrivateTime("activatedAt");
                yield return Wait(0.4f); Check(runner.CastCount == 0, "Debug reset bypassed initial delay.");
                while (runner.CastCount == 0) { Check(Time.time - activated < 1.1f, "Debug reset did not rearm the ability."); yield return null; }
                Check(runner.LastCastStartedAt - activated >= 0.79f, "Debug reset cast began early.");
                runner.enabled = false; runner.enabled = true; activated = PrivateTime("activatedAt");
                yield return Wait(0.4f); Check(runner.CastCount == 0 && runner.SetupError == null, "Reactivation retained an error or bypassed initial delay.");
                while (runner.CastCount == 0) { Check(Time.time - activated < 1.1f, "Reactivation did not rearm the ability."); yield return null; }
                definition.Width = float.NaN; yield return Wait(0.15f);
                Check(runner.SetupError != null && runner.SetupError.Contains("width"), "Nonfinite width was accepted.");
                definition.Width = 4f; definition.Length = float.PositiveInfinity; yield return Wait(0.15f);
                Check(runner.SetupError != null && runner.SetupError.Contains("length"), "Nonfinite length was accepted.");
                definition.Length = original.Length;

                // Repair must preserve the completion cooldown, not just the interval.
                definition.InitialDelaySeconds = 0f; definition.IntervalSeconds = 0.1f; definition.CooldownSeconds = 1f;
                combat.ResetSession(units); yield return Wait(0.15f);
                Check(runner.CastCount == 1, "Cooldown repair fixture did not cast.");
                float cooldownStart = runner.LastCastStartedAt;
                definition.Width = 0f; yield return Wait(0.2f); definition.Width = 4f; yield return Wait(0.3f);
                Check(runner.SetupError == null && runner.CastCount == 1, "Repair discarded the completion cooldown.");
                while (runner.CastCount == 1) { Check(Time.time - cooldownStart < 1.3f, "Repair restarted the completion cooldown."); yield return null; }
                Check(runner.LastCastStartedAt - cooldownStart >= 0.99f, "Repair bypassed the completion cooldown.");

                definition.TriggerMode = AbilityTrigger.HealthThreshold; definition.HealthThresholdPercent = 50f;
                definition.CooldownSeconds = 0f; combat.ResetSession(units);
                combat.ApplyDamage(null, boss, boss.MaximumHealth * 3 / 5); yield return Wait(0.15f);
                Check(runner.CastCount == 1, "Threshold repair fixture did not cast.");
                definition.Width = 0f; yield return Wait(0.15f); definition.Width = 4f; yield return Wait(0.3f);
                Check(runner.SetupError == null && runner.CastCount == 1, "Repair rearmed a consumed health threshold.");
                Debug.Log("T8 STARTUP: fresh saved-loadout cast at " + (firstStart) + " seconds; frames=" + (Time.frameCount - firstFrame)
                    + "; boundary/dimension/dependency recovery, warning deduplication, interval/cooldown/threshold/reset/reactivation clocks passed.");
            }
            finally { box.enabled = true; box.size = savedSize; }
        }
        private static float PrivateTime(string field) => (float)typeof(BossAbilityRunner)
            .GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(runner);
        private static IEnumerator Geometry()
        {
            foreach (Vector3 origin in new[] { Vector3.zero, new Vector3(15f, 0f, 15f), new Vector3(-15f, 0f, -15f) })
                for (int angle = 0; angle < 360; angle += 7)
                {
                    Vector3 forward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    var footprint = HazardFootprint.Rectangle(origin, forward, 4f, 100f, boundary);
                    Check(footprint.Vertices.Count >= 3, "Clipped footprint vanished.");
                    foreach (Vector3 vertex in footprint.Vertices)
                    {
                        Check(Mathf.Abs(vertex.x) <= 16.501f && Mathf.Abs(vertex.z) <= 16.501f, "Corner footprint escaped explicit boundary.");
                        Check(footprint.Contains(vertex), "Closed polygon excluded its own edge/corner.");
                    }
                }
            var f = HazardFootprint.Rectangle(Vector3.zero, Vector3.forward, 4f, 10f, boundary);
            Check(f.Contains(new Vector3(2f, 90f, 5f)) && !f.Contains(new Vector3(2.02f, 0f, 5f)), "Ground-center edge membership is incorrect.");
            Check(f.Contains(new Vector3(2.5f, 0f, 5f), 0.8f) && f.IntersectsSegment(new Vector3(-4f,0f,5f),new Vector3(4f,0f,5f),0f), "Clearance/route intersection failed.");
            definition.Width = 0f; Check(definition.ValidationError(true) != null, "Zero width accepted."); definition.Width = 4f;
            definition.Shape = (AbilityShape)99; Check(definition.ValidationError(true) != null, "Unsupported shape accepted."); definition.Shape = AbilityShape.Rectangle;
            definition.HealthThresholdPercent = 101f; Check(definition.ValidationError(true) != null, "Invalid health threshold accepted."); definition.HealthThresholdPercent = 50f;
            definition.CastTimeSeconds = -1f; Check(definition.ValidationError(true) != null, "Negative duration accepted."); definition.CastTimeSeconds = 3f;
            definition.AllowedTargetRoles = 0; Check(definition.ValidationError(true) != null, "Empty target filter accepted."); definition.AllowedTargetRoles = TargetRoles.All;
            definition.AllowedTargetRoles = (TargetRoles)(-1);
            Check(definition.ValidationError(true) == null && brains.All(b=>definition.Allows(b.Role)), "Inspector Everything did not select all four roles.");
            definition.AllowedTargetRoles = (TargetRoles)16; Check(definition.ValidationError(true) != null, "Unsupported role bit accepted.");
            definition.AllowedTargetRoles = TargetRoles.All;
            Check(definition.ValidationError(false) != null, "Missing boundary accepted.");
            Debug.Log("T8 CHECK: clipped rectangle, closed edges, clearance and invalid setup passed."); yield return null;
        }
        private static IEnumerator TargetingAndMovement()
        {
            Reset(false); Warp(boss, Vector3.zero); Warp(party[0],new Vector3(-8,0,-10)); Warp(party[1],new Vector3(0,0,-4)); Warp(party[2],new Vector3(7,0,-4)); Warp(party[3],new Vector3(-9,0,-4));
            controller.enabled = true; yield return Wait(0.3f);
            Check(controller.CombatTarget == party[1], "Boss did not acquire closest unit.");
            Warp(party[0],new Vector3(4,0,0)); Warp(party[1],new Vector3(0,0,-10)); Warp(party[2],new Vector3(-4,0,0)); yield return Wait(0.3f);
            Check(controller.CombatTarget == party[0], "Encounter ID did not break closest-target tie.");
            Warp(party[0],new Vector3(-8,0,-10)); yield return Wait(0.3f);
            Check(controller.CombatTarget == party[2], "Unacquired boss did not reevaluate nearest target.");
            combat.ApplyDamage(party[0],boss,1); combat.ApplyDamage(party[1],boss,1); yield return Wait(0.3f);
            Check(controller.CombatTarget == party[0], "Positive Tank damage failed to lock target or another role stole it.");
            combat.ApplyDamage(null,party[0],int.MaxValue); yield return Wait(0.3f);
            Check(controller.CombatTarget != party[0] && controller.CombatTarget != null, "Dead Tank did not release lock.");
            foreach(var member in party) combat.ApplyDamage(null,member,int.MaxValue); yield return Wait(0.3f);
            Check(controller.CombatTarget == null && controller.State == "Idle", "No-target boss did not become idle.");

            Reset(true); Warp(boss,new Vector3(0,0,8)); Warp(party[0],new Vector3(0,0,-3));
            for(int i=1;i<4;i++) combat.ApplyDamage(null,party[i],int.MaxValue);
            controller.enabled=true; float deadline=Time.time+12f;
            while(CombatPositionSearch.PlanarDistance(boss.transform.position,party[0].transform.position)>boss.MeleeRange)
            { Check(Time.time<deadline,"Boss did not approach into melee range."); CheckBossClearance(); yield return null; }
            yield return Wait(0.8f);
            var agent=boss.GetComponent<NavMeshAgent>();
            Check(CombatPositionSearch.PlanarDistance(boss.transform.position,party[0].transform.position)<=boss.MeleeRange,"Boss overshot range band.");
            Vector3 direction=(party[0].transform.position-boss.transform.position).normalized;
            Warp(party[0],boss.transform.position+direction*0.7f); yield return Wait(0.3f);
            Check(controller.IsRetreating && Mathf.Abs(agent.speed-1.5f)<0.01f,"Close target did not trigger half-speed retreat.");
            Check(Vector3.Dot(boss.transform.forward,direction)>0.9f,"Retreat flipped boss facing.");
            yield return Wait(1f);
            CheckBossClearance();
            Check(boundary.Contains(boss.transform.position),"Boss left playable boundary.");
            if (boss.GetComponent<UnitMovement>().State == MovementState.Moving)
                    Check(CombatPositionSearch.PlanarDistance(boss.GetComponent<UnitMovement>().Destination,party[0].transform.position)>=1.35f-0.01f,"Retreat selected a destination below its release distance.");

            Reset(false); Warp(boss,Vector3.zero); Warp(party[0],Vector3.back); for(int i=1;i<4;i++) combat.ApplyDamage(null,party[i],int.MaxValue);
            controller.enabled=true; events.Clear(); float activation=Time.time;
            yield return Wait(2.7f); Check(!events.Any(e=>e.hit.Source==boss&&e.hit.Damage>0),"Boss swung before initial three seconds.");
            yield return Wait(7f);
            var swings=events.Where(e=>e.hit.Source==boss&&e.hit.Type==CombatEventType.SwingDamage).ToArray();
            Check(swings.Length>=3 && swings[0].time-activation>=2.99f,"Boss cadence fixture did not swing.");
            for(int i=1;i<swings.Length;i++) Check(Mathf.Abs(swings[i].time-swings[i-1].time-3f)<0.25f,"Boss swing interval is not three seconds.");
            Warp(party[0],new Vector3(12,0,-10)); int count=swings.Length; yield return Wait(6.2f);
            Check(events.Count(e=>e.hit.Source==boss&&e.hit.Damage>0)==count,"Boss hit out of range.");
            Warp(party[0],Vector3.back); yield return Wait(0.2f); int after=events.Count(e=>e.hit.Source==boss&&e.hit.Damage>0); yield return Wait(0.6f);
            Check(after==count+1 && events.Count(e=>e.hit.Source==boss&&e.hit.Damage>0)==after,"Boss banked a catch-up burst.");
            Debug.Log("T8 CHECK: targeting, Tank lock, fallback, approach/retreat/facing and three-second swings passed.");
        }
        private static IEnumerator SpacingStability()
        {
            Reset(true); Warp(boss, new Vector3(0, 0, 8)); Warp(party[0], new Vector3(0, 0, -3));
            for (int i = 1; i < party.Length; i++) combat.ApplyDamage(null, party[i], int.MaxValue);
            brains[0].enabled = true; controller.enabled = true;
            float deadline = Time.time + 15f;
            while (CombatPositionSearch.PlanarDistance(boss.transform.position, party[0].transform.position) > party[0].MeleeRange
                || controller.State != "In melee range")
            { Check(Time.time < deadline, "Autonomous Tank and boss never settled into mutual melee range."); CheckBossClearance(); yield return null; }
            yield return Wait(0.5f);
            Vector3 settledBoss = boss.transform.position;
            events.Clear(); float end = Time.time + 9f; int firstFrame = Time.frameCount;
            while (Time.time < end)
            {
                Check(!controller.IsRetreating && !controller.IsChasing, "Normal Tank approach triggered a chase/retreat loop.");
                Check(CombatPositionSearch.PlanarDistance(boss.transform.position, settledBoss) < 0.08f, "Boss drifted while both units could attack.");
                Check(CombatPositionSearch.PlanarDistance(boss.transform.position, party[0].transform.position) <= Mathf.Min(boss.MeleeRange, party[0].MeleeRange) + 0.01f,
                    "Mutual melee range was not stable.");
                CheckBossClearance(); yield return null;
            }
            Check(events.Any(e => e.hit.Source == boss && e.hit.Type == CombatEventType.SwingDamage)
                && events.Any(e => e.hit.Source == party[0] && e.hit.Type == CombatEventType.SwingDamage), "Settled units did not both continue attacking.");
            Debug.Log("T8 SPACING: autonomous Tank/boss held mutual range for nine seconds while both attacked; distance="
                + CombatPositionSearch.PlanarDistance(boss.transform.position, party[0].transform.position) + "; frames=" + (Time.frameCount - firstFrame));

            Reset(true); Warp(boss, Vector3.zero); Warp(party[0], new Vector3(0, 0, 2));
            for (int i = 1; i < party.Length; i++) combat.ApplyDamage(null, party[i], int.MaxValue);
            controller.enabled = true; yield return Wait(0.5f);
            Vector3 held = boss.transform.position;
            Check(!controller.IsRetreating && !controller.IsChasing && CombatPositionSearch.PlanarDistance(held, Vector3.zero) < 0.05f,
                "Stationary Tank at its attack range displaced the boss.");
            // Stay just inside the edge: NavMesh sampling can round an exact
            // range coordinate a few float units beyond legal attack reach.
            Warp(party[0], new Vector3(0, 0, boss.MeleeRange - 0.01f)); yield return Wait(0.5f);
            Check(!controller.IsChasing && CombatPositionSearch.PlanarDistance(boss.transform.position, held) < 0.05f,
                "Target at the outer melee edge triggered unnecessary approach; distance="
                + CombatPositionSearch.PlanarDistance(boss.transform.position, party[0].transform.position)
                + ", sight=" + combat.HasLineOfSight(boss.transform.position, party[0].transform.position));

            Warp(party[0], boss.transform.position + Vector3.forward * 0.7f);
            deadline = Time.time + 0.5f;
            while (!controller.IsRetreating) { Check(Time.time < deadline, "Genuinely close Tank did not trigger retreat."); yield return null; }
            var agent = boss.GetComponent<NavMeshAgent>();
            Check(Mathf.Abs(agent.speed - 1.5f) < 0.01f && Vector3.Dot(boss.transform.forward, Vector3.forward) > 0.9f,
                "Retreat speed/facing changed.");
            deadline = Time.time + 3f;
            while (controller.IsRetreating) { Check(Time.time < deadline, "Retreat stalled before its release threshold."); yield return null; }
            float releasedDistance = CombatPositionSearch.PlanarDistance(boss.transform.position, party[0].transform.position);
            Check(releasedDistance >= 1.35f - 0.02f && releasedDistance <= party[0].MeleeRange, "Retreat did not release inside mutual reach.");
            held = boss.transform.position; yield return Wait(0.6f);
            Check(CombatPositionSearch.PlanarDistance(boss.transform.position, held) < 0.05f, "Completed retreat kept walking backward.");
            Warp(party[0], boss.transform.position + Vector3.forward * 1.15f); yield return Wait(0.4f);
            Check(!controller.IsRetreating && CombatPositionSearch.PlanarDistance(boss.transform.position, held) < 0.05f,
                "Hysteresis gap restarted retreat without crossing entry threshold.");
            Warp(party[0], boss.transform.position + Vector3.forward * 7f); yield return Wait(0.3f);
            Check(controller.IsChasing && Mathf.Abs(agent.speed - 3f) < 0.01f, "Out-of-range target did not restore forward pursuit.");

            Reset(true); Warp(boss, Vector3.zero); Warp(party[0], Vector3.forward * 0.7f); Warp(party[1], Vector3.forward * 8f);
            for (int i = 2; i < party.Length; i++) combat.ApplyDamage(null, party[i], int.MaxValue);
            controller.enabled = true; deadline = Time.time + 0.5f;
            while (!controller.IsRetreating) { Check(Time.time < deadline, "Target-change fixture did not start retreat."); yield return null; }
            float newEntry = Mathf.Min(boss.MeleeRange, party[1].MeleeRange) * 0.5f;
            Warp(party[1], boss.transform.position + Vector3.forward * (newEntry + 0.175f));
            combat.ApplyDamage(null, party[0], int.MaxValue); yield return Wait(0.3f);
            Check(controller.CombatTarget == party[1] && !controller.IsRetreating && !controller.IsChasing,
                "Target change retained the previous target's retreat hysteresis.");
            Debug.Log("T8 SPACING: mutual/outer-range hold, half-speed close retreat, release/hysteresis, renewed pursuit and target-change reset passed.");
        }
        private static IEnumerator Abilities()
        {
            Reset(false); Warp(boss,Vector3.zero); Warp(party[0],new Vector3(0,0,10)); Warp(party[1],new Vector3(0,0,3)); Warp(party[2],new Vector3(2,0,3)); Warp(party[3],new Vector3(3,0,3));
            definition.InitialDelaySeconds=0.4f; definition.IntervalSeconds=1.5f; definition.CastTimeSeconds=0.5f;
            definition.AllowedTargetRoles=TargetRoles.Tank; SetLoadout(definition); controller.enabled=true; runner.enabled=true;
            float deadline=Time.time+2f; while(!runner.IsCasting) { Check(Time.time<deadline,"Wall did not start."); yield return null; }
            Vector3 origin=runner.CastOrigin, forward=runner.CastForward, position=boss.transform.position;
            Check(hazards.Count==1 && runner.PendingFootprint.Contains(party[1].transform.position),"Pending telegraph does not match hit geometry.");
            Warp(party[0],new Vector3(10,0,10)); combat.ApplyDamage(party[0],boss,1);
            combat.ApplyDamage(null,party[2],party[2].CurrentHealth-50); events.Clear();
            yield return Wait(0.25f);
            Check(runner.IsCasting && boss.transform.position==position && Vector3.Dot(boss.transform.forward,forward)>0.999f,"Cast origin/facing moved after Tank acquisition.");
            yield return Wait(0.35f);
            Check(hazards.Count==0 && !runner.IsCasting && !boss.FacingLocked,"Resolved impact remained pending or facing stayed locked.");
            var damage=events.Where(e=>e.hit.Type==CombatEventType.SpellDamage).ToArray();
            Check(damage.Length==2 && damage.Any(e=>e.hit.Target==party[1]&&e.hit.Damage==95)
                && damage.Any(e=>e.hit.Target==party[2]&&e.hit.Damage==50),"One-shot side/inside/outside damage or effective lethal amount failed.");
            Check(events.First(e=>e.hit.Type!=CombatEventType.SwingDamage).hit.Type==CombatEventType.SpellCastSuccess
                && events.Any(e=>e.hit.Type==CombatEventType.UnitDied&&e.hit.SpellId==definition.AbilityId),"Spell identity or success/damage/death ordering failed.");
            float first=runner.LastCastStartedAt; deadline=Time.time+2f;
            while(runner.CastCount<2) { Check(Time.time<deadline,"Wall did not recur."); yield return null; }
            Check(Mathf.Abs(runner.LastCastStartedAt-first-1.5f)<0.22f,"Interval is not measured between cast starts.");
            runner.enabled=false; Check(hazards.Count==0 && !boss.FacingLocked,"Disable did not cancel pending cast.");

            Reset(false); Warp(boss,Vector3.zero); Warp(party[0],new Vector3(-12,0,-10)); Warp(party[1],new Vector3(0,0,5));
            Warp(party[2],new Vector3(10,0,10)); Warp(party[3],new Vector3(-12,0,10));
            SetLoadout(definition); controller.enabled=true; runner.enabled=true;
            deadline=Time.time+2f; while(!runner.IsCasting) { Check(Time.time<deadline,"Target-restore fixture did not begin."); yield return null; }
            Check(controller.CombatTarget==party[1],"Pre-cast combat target fixture is wrong.");
            Warp(party[2],Vector3.forward); yield return Wait(0.25f);
            Check(controller.CombatTarget==party[1],"Closest-unit reevaluation replaced a valid pre-cast target during casting.");
            while(runner.IsCasting) yield return null;
            Check(controller.CombatTarget==party[1],"Cast did not restore the valid pre-cast combat target.");

            Reset(false); definition.DeliveryMode=AbilityDelivery.DirectTarget; definition.TriggerMode=AbilityTrigger.HealthThreshold;
            definition.InitialDelaySeconds=0f; definition.CastTimeSeconds=0f; definition.Damage=1; definition.AllowedTargetRoles=TargetRoles.Healer;
            SetLoadout(definition); controller.enabled=true; runner.enabled=true;
            party[1].gameObject.SetActive(false); combat.ApplyDamage(null,boss,boss.MaximumHealth*3/5); yield return Wait(0.3f);
            Check(runner.CastCount==0,"No eligible target produced a phantom cast.");
            combat.ApplyHealing(null,boss,boss.MaximumHealth); party[1].gameObject.SetActive(true); yield return Wait(0.3f);
            Check(runner.CastCount==1 && !runner.IsCasting,"Pending threshold/zero-time cast did not resolve once.");
            var castEvents=events.Where(e=>e.hit.SpellId==definition.AbilityId).ToArray();
            Check(castEvents.Length==3 && castEvents[0].hit.Type==CombatEventType.SpellCastStart
                && castEvents[1].hit.Type==CombatEventType.SpellCastSuccess && castEvents[2].hit.Type==CombatEventType.SpellDamage,"Zero-time direct event ordering failed.");
            combat.ApplyDamage(null,boss,boss.MaximumHealth*3/5); yield return Wait(0.4f); Check(runner.CastCount==1,"Healing rearmed consumed threshold.");
            combat.ResetSession(units); combat.ApplyDamage(null,boss,boss.MaximumHealth*3/5); yield return Wait(0.3f); Check(runner.CastCount==1,"Reset did not rearm threshold.");

            Reset(false); definition.TriggerMode=AbilityTrigger.Interval; definition.InitialDelaySeconds=0.1f; definition.IntervalSeconds=1f;
            definition.CastTimeSeconds=0.5f; definition.AllowedTargetRoles=TargetRoles.Tank; SetLoadout(definition); controller.enabled=true; runner.enabled=true;
            yield return Wait(0.25f); Check(runner.IsCasting,"Direct cancellation fixture did not start.");
            combat.ApplyDamage(null,party[0],int.MaxValue); events.Clear(); yield return Wait(0.5f);
            Check(events.Any(e=>e.hit.Type==CombatEventType.SpellCastFailed) && !events.Any(e=>e.hit.Type==CombatEventType.SpellDamage),"Lost direct target did not fail without damage.");
            combat.ResetSession(units); yield return Wait(0.2f); Check(runner.IsCasting,"Reset did not rearm initial delay.");
            combat.ResetSession(units);
            Check(!runner.IsCasting && hazards.Count==0 && combat.LogService.History.Count==0 && !boss.FacingLocked,"Reset left stale cast/hazard/log/facing state.");
            Debug.Log("T8 CHECK: Wall hit snapshot, locks, start cadence, spell events, threshold/direct/zero-time and cancellation/reset passed.");
        }
        private static IEnumerator Reactions()
        {
            // T8's original escape fixtures explicitly model guaranteed detection under T10.
            foreach (var brain in brains) brain.SetAwarenessRollSourceForVerification(() => 1);
            Reset(false); Warp(boss,new Vector3(0,0,-2));
            for(int i=0;i<party.Length;i++) Warp(party[i],new Vector3(0,0,i*2f));
            var footprint=HazardFootprint.Rectangle(new Vector3(0,0,-1),Vector3.forward,4f,14f,boundary);
            object owner=new object(); hazards.Register(owner,footprint);
            foreach(var brain in brains) brain.enabled=true;
            yield return Wait(0.25f); Check(brains.All(b=>b.State==RoleState.Escaping),"Not all roles chose escape.");
            var mover=party[0].GetComponent<UnitMovement>();
            brains[0].enabled=false; Warp(party[0],Vector3.zero); Warp(boss,new Vector3(0,0,-1));
            Check(mover.TryMove(new Vector3(0,0,8),MovementSource.Manual)==MoveResult.Accepted,"Manual hazard move rejected.");
            int command=mover.CommandId; events.Clear(); brains[0].enabled=true; yield return Wait(0.25f);
            Check(mover.HasManualOverride && mover.CommandId==command && brains[0].State==RoleState.ManualMove,"Escape replaced manual movement.");
            Check(events.Any(e=>e.hit.Source==party[0]&&e.hit.Damage>0),"Manual hazard move suppressed valid combat.");
            mover.Cancel(); yield return Wait(3f);
            Check(party.All(p=>!hazards.IsUnsafe(p.transform.position,p.GetComponent<NavMeshAgent>().radius+0.15f)),"Escape stopped without agent clearance.");
            yield return Wait(0.8f); Check(party.All(p=>!hazards.IsUnsafe(p.transform.position)),"Normal role positioning reentered warning.");
            hazards.Remove(owner);
            Reset(false); Warp(party[0],new Vector3(0,0,3)); hazards.Register(owner,footprint);
            object overlap=new object(); hazards.Register(overlap,HazardFootprint.Rectangle(new Vector3(-10,0,3),Vector3.right,4f,20f,boundary));
            Check(hazards.TryEscape(party[0].GetComponent<NavMeshAgent>(),out Vector3 exit) && !hazards.IsUnsafe(exit,0.8f),"Overlap search failed to find a safe reachable exit.");
            hazards.Remove(overlap); hazards.Remove(owner);
            hazards.Register(owner,HazardFootprint.Rectangle(new Vector3(0,0,-16.5f),Vector3.forward,100f,33f,boundary));
            brains[0].enabled=true; yield return Wait(0.3f); Check(brains[0].State==RoleState.NoSafeExit,"Unavoidable hazard did not expose NoSafeExit.");
            hazards.Remove(owner); brains[0].enabled=false;
            Reset(true); Warp(boss,new Vector3(0,0,8)); Warp(party[1],new Vector3(0,0,2)); Warp(party[0],new Vector3(-8,0,-10));
            Warp(party[2],new Vector3(10,0,-10)); Warp(party[3],new Vector3(-12,0,-10));
            combat.ApplyDamage(null,party[0],1); controller.enabled=true; brains[1].enabled=true; events.Clear(); yield return Wait(0.4f);
            Check(controller.CombatTarget==party[1] && controller.IsChasing && brains[1].State==RoleState.RetreatingToTank,"Non-Tank chase did not trigger Tank retreat.");
            Check(events.Any(e=>e.hit.Source==party[1]&&e.hit.Healing>0),"Retreat suppressed valid healing.");
            combat.ApplyDamage(null,party[0],int.MaxValue); yield return Wait(0.3f); Check(brains[1].State!=RoleState.RetreatingToTank,"Retreat retained a dead Tank.");
            Reset(false); Warp(boss,Vector3.zero); Warp(party[0],Vector3.back); brains[0].enabled=true; yield return Wait(0.25f);
            boss.gameObject.SetActive(false); events.Clear(); yield return Wait(0.3f);
            Check(brains[0].State==RoleState.Idle && !events.Any(e=>e.hit.Source==party[0]&&e.hit.Damage>0)
                && party[0].GetComponent<UnitMovement>().State!=MovementState.Moving,"Inactive boss retained party combat or positioning.");
            boss.gameObject.SetActive(true); yield return Wait(0.3f); Check(brains[0].State!=RoleState.Idle,"Party did not resume after boss reactivation.");
            Debug.Log("T8 CHECK: all-role escape/clearance, manual combat/override, overlap, unreachable exit and Tank retreat passed.");
        }
        private static IEnumerator LoadoutAndIsolation()
        {
            Reset(false); definition.TriggerMode=AbilityTrigger.Interval; definition.DeliveryMode=AbilityDelivery.DirectTarget;
            definition.InitialDelaySeconds=0f; definition.IntervalSeconds=0.1f; definition.CooldownSeconds=0f;
            definition.CastTimeSeconds=0f; definition.Damage=0; definition.AllowedTargetRoles=TargetRoles.All;
            SetLoadout(definition); controller.enabled=true; runner.enabled=true;
            yield return Wait(5f);
            Check(party.All(p=>events.Any(e=>e.hit.Type==CombatEventType.SpellCastStart&&e.hit.Target==p)),"Random selection never aimed at every eligible role.");
            SetLoadout(); int count=runner.CastCount; yield return Wait(0.3f); Check(runner.CastCount==count,"Removing loadout did not stop casts.");
            var second=Object.Instantiate(definition); second.AbilityId=2; second.IntervalSeconds=20f;
            definition.IntervalSeconds=20f;
            try
            {
                Reset(false); SetLoadout(definition,second); controller.enabled=true; runner.enabled=true;
                yield return Wait(0.3f);
                Check(events.Where(e=>e.hit.Type==CombatEventType.SpellCastStart).Select(e=>e.hit.SpellId).SequenceEqual(new int?[]{1,2}),"Simultaneous readiness did not preserve loadout order.");
                runner.enabled=false; SetLoadout(definition); definition.InitialDelaySeconds=0.6f;
                controller.enabled=true; runner.enabled=true;
                GameObject clone=Object.Instantiate(boss.gameObject); clone.name="T8 isolated boss fixture";
                var cloneRunner=clone.GetComponent<BossAbilityRunner>(); cloneRunner.enabled=false;
                clone.GetComponent<UnitMovement>().enabled=false;
                try
                {
                    yield return Wait(0.35f); cloneRunner.enabled=true;
                    yield return Wait(0.4f);
                    Check(runner.CastCount==1 && cloneRunner.CastCount==0,"Boss instances shared initial delay/readiness.");
                    yield return Wait(0.35f); Check(cloneRunner.CastCount==1,"Second boss did not use its own activation clock.");
                }
                finally { Object.Destroy(clone); }
                Reset(false); definition.InitialDelaySeconds=0f; definition.IntervalSeconds=0.1f; definition.CooldownSeconds=0.6f; definition.CastTimeSeconds=0.2f;
                SetLoadout(definition); controller.enabled=true; runner.enabled=true;
                yield return Wait(1.9f);
                var starts=events.Where(e=>e.hit.Type==CombatEventType.SpellCastStart).ToArray();
                Check(starts.Length>=2 && starts[1].time-starts[0].time>=0.79f,"Cooldown and interval were not independent gates.");
                runner.enabled=false;
                // A single allowed role with two eligible units must select both units.
                var ally=Object.Instantiate(party[2].gameObject); ally.name="T8 extra melee fixture";
                ally.GetComponent<PartyRoleBrain>().enabled=false;
                try
                {
                    Reset(false); definition.InitialDelaySeconds=0f; definition.IntervalSeconds=0.1f; definition.CooldownSeconds=0f;
                    definition.CastTimeSeconds=0f; definition.AllowedTargetRoles=TargetRoles.MeleeDps;
                    var settings=new SerializedObject(runner); var members=settings.FindProperty("party"); members.arraySize=5;
                    for(int i=0;i<4;i++) members.GetArrayElementAtIndex(i).objectReferenceValue=brains[i];
                    members.GetArrayElementAtIndex(4).objectReferenceValue=ally.GetComponent<PartyRoleBrain>(); settings.ApplyModifiedPropertiesWithoutUndo();
                    SetLoadout(definition); controller.enabled=true; runner.enabled=true; yield return Wait(3f);
                    Check(events.Any(e=>e.hit.Type==CombatEventType.SpellCastStart&&e.hit.Target==party[2])
                        && events.Any(e=>e.hit.Type==CombatEventType.SpellCastStart&&e.hit.Target==ally.GetComponent<CombatUnit>()),"Single-role targeting selected roles instead of units.");
                }
                finally
                {
                    runner.enabled=false; var settings=new SerializedObject(runner); settings.FindProperty("party").arraySize=4; settings.ApplyModifiedPropertiesWithoutUndo(); Object.Destroy(ally);
                }
            }
            finally { Object.Destroy(second); }
            Debug.Log("T8 CHECK: random roles/units, loadout add/remove/order, independent cooldowns and multi-boss activation passed.");
        }
        private static IEnumerator GroundLifecycle()
        {
            Reset(false); Warp(boss,Vector3.zero); Warp(party[0],new Vector3(0,0,10)); Warp(party[1],new Vector3(0,0,4));
            Warp(party[2],new Vector3(8,0,-8)); Warp(party[3],new Vector3(-8,0,-8));
            definition.TriggerMode=AbilityTrigger.Interval; definition.DeliveryMode=AbilityDelivery.GroundArea;
            definition.InitialDelaySeconds=0.1f; definition.IntervalSeconds=20f; definition.CastTimeSeconds=0.5f;
            definition.AllowedTargetRoles=TargetRoles.Tank; definition.Damage=95;
            var extraCollider=party[1].gameObject.AddComponent<BoxCollider>();
            var visual=Object.FindAnyObjectByType<GroundHazardVisual>();
            var log=combat.LogService; var settings=new SerializedObject(combat);
            try
            {
                visual.enabled=false; settings.FindProperty("logService").objectReferenceValue=null; settings.ApplyModifiedPropertiesWithoutUndo();
                SetLoadout(definition); controller.enabled=true; runner.enabled=true;
                yield return Wait(0.25f); Check(runner.IsCasting,"Ground lifecycle fixture did not begin.");
                combat.ApplyDamage(null,party[0],int.MaxValue); events.Clear(); yield return Wait(0.5f);
                Check(events.Any(e=>e.hit.Type==CombatEventType.SpellCastSuccess)
                    && events.Count(e=>e.hit.Type==CombatEventType.SpellDamage&&e.hit.Target==party[1])==1,"Ground target death, collider deduplication or independent resolution failed.");
                int health=party[1].CurrentHealth; yield return Wait(0.5f); Check(party[1].CurrentHealth==health,"Impact caused lingering damage.");
                Reset(false); SetLoadout(definition); controller.enabled=true; runner.enabled=true; yield return Wait(0.25f);
                foreach(var member in party) Warp(member,new Vector3(-12,0,-10)); events.Clear(); yield return Wait(0.5f);
                Check(events.Any(e=>e.hit.Type==CombatEventType.SpellCastSuccess) && !events.Any(e=>e.hit.Type==CombatEventType.SpellDamage),"Zero-hit ground cast did not succeed.");
                combat.ResetSession(units); yield return Wait(0.25f); Check(runner.IsCasting,"Death-cancel fixture did not begin.");
                combat.KeepBossAlive=false; combat.ApplyDamage(null,boss,int.MaxValue); events.Clear(); yield return Wait(0.6f);
                Check(!runner.IsCasting && hazards.Count==0 && !events.Any(e=>e.hit.Type==CombatEventType.SpellDamage),"Boss death left stale damage/hazard state.");
            }
            finally
            {
                settings.FindProperty("logService").objectReferenceValue=log; settings.ApplyModifiedPropertiesWithoutUndo(); visual.enabled=true; Object.Destroy(extraCollider);
            }
            Reset(false); Warp(party[0],new Vector3(-3.5f,0,3f));
            object owner=new object(); hazards.Register(owner,HazardFootprint.Rectangle(new Vector3(-3.5f,0,-2),Vector3.forward,4f,12f,boundary));
            Check(hazards.TryEscape(party[0].GetComponent<NavMeshAgent>(),out Vector3 destination)
                && !hazards.IsUnsafe(destination,0.8f),"Obstacle beside nearest exit prevented all alternate reachable exits."); hazards.Remove(owner);
            Debug.Log("T8 CHECK: lost ground aim, collider deduplication, zero-hit success, no lingering damage, independent presentation/logging, boss-death cleanup and obstructed exits passed.");
        }
        private static IEnumerator Production()
        {
            // Use the agreed 90-second baseline without overwriting authored test timing.
            Reset(true); definition.InitialDelaySeconds = 90f; definition.IntervalSeconds = 90f; SetLoadout(definition);
            foreach(var unit in units) unit.SetMaximumHealth(20000); combat.ResetSession(units); combat.KeepBossAlive=true;
            foreach(var brain in brains) brain.enabled=true; controller.enabled=true; runner.enabled=true;
            float activation=Time.time, wallStart=-1f;
            while(!runner.IsCasting)
            {
                CheckBossClearance();
                Check(Time.time-activation<91f,"Production Wall did not start at 90 seconds.");
                if(Time.time-activation<89.95f) Check(runner.CastCount==0,"Production Wall started early.");
                yield return null;
            }
            wallStart=runner.LastCastStartedAt;
            Check(wallStart-activation>=89.95f && wallStart-activation<90.3f,"Production initial delay drifted.");
            yield return CaptureViews("yellow");
            while(runner.IsCasting) { Check(Time.time-wallStart<3.3f,"Production cast exceeded three seconds."); yield return null; }
            Check(Time.time-wallStart>=2.99f && hazards.Count==0,"Production cast duration or hazard release failed.");
            yield return CaptureViews("red");
            Debug.Log("T8 PRODUCTION: first Wall started after "+(wallStart-activation)+" seconds and resolved after "+(Time.time-wallStart)+" seconds; frames="+Time.frameCount);
        }
        private static IEnumerator CaptureViews(string phase)
        {
            float scale=Time.timeScale; Time.timeScale=0f;
            Camera camera=Camera.main; float size=camera.orthographicSize;
            var controls=camera.GetComponents<MonoBehaviour>().Where(c=>c.GetType().Namespace=="RaidCommander.CameraControl").ToArray();
            foreach(var control in controls) control.enabled=false;
            try
            {
                foreach(float zoom in new[]{8f,20f,30f})
                {
                    camera.orthographicSize=zoom;
                    yield return null; yield return null;
                    ScreenCapture.CaptureScreenshot("Logs/t8-"+phase+"-"+zoom+".png");
                    yield return null; yield return null; yield return null;
                }
            }
            finally
            {
                camera.orthographicSize=size; foreach(var control in controls) control.enabled=true; Time.timeScale=scale;
            }
        }
        private static void Reset(bool mobile)
        {
            PartyAiVerification.SuspendAutonomy();
            boss.gameObject.SetActive(true);
            foreach(var member in party) { member.gameObject.SetActive(true); member.SetMaximumHealth(1000); }
            boss.SetMaximumHealth(5000); combat.ResetSession(units); combat.KeepBossAlive=true;
            boss.GetComponent<UnitMovement>().enabled=mobile;
            Warp(boss,new Vector3(0,0,8));
            Warp(party[0],new Vector3(-2,0,-5)); Warp(party[1],new Vector3(0,0,-7)); Warp(party[2],new Vector3(2,0,-5)); Warp(party[3],new Vector3(-3,0,-7)); events.Clear();
        }
        private static void SetLoadout(params BossAbilityDefinition[] definitions)
        {
            var settings=new SerializedObject(runner); var list=settings.FindProperty("loadout"); list.arraySize=definitions.Length;
            for(int i=0;i<definitions.Length;i++) list.GetArrayElementAtIndex(i).objectReferenceValue=definitions[i]; settings.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Warp(CombatUnit unit,Vector3 point)
        {
            point.y=boundary.GroundY;
            var agent=unit.GetComponent<NavMeshAgent>(); unit.GetComponent<UnitMovement>().Cancel();
            Check(NavMesh.SamplePosition(point,out NavMeshHit hit,1.5f,new NavMeshQueryFilter { agentTypeID=agent.agentTypeID,areaMask=agent.areaMask }),"Fixture off NavMesh: "+point);
            Check(agent.Warp(hit.position+Vector3.up*agent.baseOffset),"Warp failed: "+unit.name); agent.ResetPath();
        }
        private static CombatUnit Unit(string name)=>GameObject.Find(name).GetComponent<CombatUnit>();
        private static void CheckBossClearance()
        {
            var body=boss.GetComponent<Collider>();
            foreach(var solid in GameObject.Find("Obstacles").GetComponentsInChildren<Collider>())
                Check(!Physics.ComputePenetration(body,body.transform.position,body.transform.rotation,solid,solid.transform.position,solid.transform.rotation,out _,out float depth)
                    || depth<0.02f,"Boss body penetrated solid geometry: "+solid.name);
            var agent=boss.GetComponent<NavMeshAgent>();
            Check(NavMesh.SamplePosition(boss.transform.position,out NavMeshHit floor,4f,new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask})
                && Mathf.Abs(body.bounds.min.y-floor.position.y)<0.15f,"Boss feet drifted from navigation floor.");
        }
        private static IEnumerator Wait(float seconds) { float end=Time.time+seconds; while(Time.time<end) yield return null; }
        private static void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
    }
}
