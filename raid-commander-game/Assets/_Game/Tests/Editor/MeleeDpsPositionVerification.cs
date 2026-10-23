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
    [InitializeOnLoad]
    public static class MeleeDpsPositionVerification
    {
        private const string Pending = "RaidCommander.T7.2.Verify";
        private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
        private static double started;
        private static int frame;
        private static bool background;
        public static string Result { get; private set; } = "Not run";

        static MeleeDpsPositionVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
                SessionState.EraseBool(Pending);
                background = Application.runInBackground; Application.runInBackground = true;
                started = EditorApplication.timeSinceStartup; frame = -1; Result = "Running";
                steps.Clear(); steps.Push(Run()); EditorApplication.update += Tick;
            };
        }

        [MenuItem("Raid Commander/Verify Melee DPS Positioning in Play Mode")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Start T7.2 verification from a saved Edit-mode scene.");
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/RaidArena.unity");
            SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup - started < 120, "T7.2 verification timed out.");
                EditorApplication.QueuePlayerLoopUpdate();
                if (Time.frameCount == frame) return;
                frame = Time.frameCount;
                while (steps.Count > 0)
                {
                    IEnumerator current = steps.Peek();
                    if (!current.MoveNext()) { (current as IDisposable)?.Dispose(); steps.Pop(); continue; }
                    if (current.Current is IEnumerator nested) { steps.Push(nested); continue; }
                    return;
                }
                Finish(true, "rear approach/hold, rotation, side/front fallback, body detour, manual and reaction priority passed");
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        private static void Finish(bool passed, string message)
        {
            EditorApplication.update -= Tick;
            while (steps.Count > 0) (steps.Pop() as IDisposable)?.Dispose();
            Application.runInBackground = background; Time.timeScale = 1f;
            Result = (passed ? "PASS: " : "FAIL: ") + message;
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText("Logs/t7.2-melee-position-verification.txt", Result);
            Debug.Log("MELEE POSITION VERIFY " + Result);
            EditorApplication.isPlaying = false;
        }

        private static IEnumerator Run()
        {
            yield return null;
            Check(Time.frameCount > 1, "Player loop did not advance.");
            Check(MeleeDpsPositionSearch.Classify(Vector3.forward, Vector3.back, 60f) == MeleeSector.Rear
                && MeleeDpsPositionSearch.Classify(Vector3.forward, Vector3.right, 60f) == MeleeSector.Side
                && MeleeDpsPositionSearch.Classify(Vector3.forward, Vector3.forward, 60f) == MeleeSector.Front
                && Mathf.Abs(MeleeDpsPositionSearch.SegmentDistance(Vector3.left, Vector3.right, Vector3.zero)) < .001f,
                "Facing sector or body-segment geometry failed.");

            var boss = GameObject.Find("Raid Boss").GetComponent<CombatUnit>();
            var melee = GameObject.Find("DPS").GetComponent<CombatUnit>();
            var tank = GameObject.Find("Tank").GetComponent<CombatUnit>();
            var combat = Object.FindAnyObjectByType<CombatSystem>();
            var hazards = Object.FindAnyObjectByType<GroundHazards>();
            var boundary = Object.FindAnyObjectByType<ArenaBoundary>();
            var brain = melee.GetComponent<PartyRoleBrain>();
            var mover = melee.GetComponent<UnitMovement>();
            var agent = melee.GetComponent<NavMeshAgent>();
            var bossAgent = boss.GetComponent<NavMeshAgent>();
            var obstacles = GameObject.Find("Obstacles").transform;
            foreach (var role in Object.FindObjectsByType<PartyRoleBrain>()) role.enabled = false;
            boss.GetComponent<BossAbilityRunner>().enabled = false;
            boss.GetComponent<BossBasicAttack>().enabled = false;
            bossAgent.updateRotation = false;
            combat.KeepBossAlive = true;
            combat.ResetSession(Object.FindObjectsByType<CombatUnit>());
            boss.transform.rotation = Quaternion.identity;
            Warp(melee, boss.transform.position + Vector3.forward * 4f);
            Check(Find(out MeleePositionPlan open) && open.Sector == MeleeSector.Rear
                && open.UsesDetour && open.FinalDestination.z < boss.transform.position.z
                && CombatPositionSearch.PlanarDistance(open.FinalDestination, boss.transform.position) <= melee.MeleeRange,
                "Open arena did not choose a reachable rear destination with a body-safe approach.");
            var hits = new List<ResolvedCombatEvent>();
            Action<ResolvedCombatEvent> record = e => hits.Add(e);
            combat.EventResolved += record;
            brain.enabled = true;
            float deadline = Time.time + 14f;
            while (!AtRear()) { Check(Time.time < deadline, "Melee DPS did not reach and hold the rear. " + Describe()); yield return null; }
            deadline = Time.time + 2f;
            while (!hits.Any(e => e.Source == melee && e.Target == boss && e.Damage > 0))
            { Check(Time.time < deadline, "Melee DPS did not attack while positioning."); yield return null; }
            int heldCommand = mover.CommandId;
            yield return Wait(1.2f);
            Check(AtRear() && mover.CommandId == heldCommand && mover.State != MovementState.Moving,
                "Stable rear position caused repeated movement.");

            boss.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            deadline = Time.time + 14f;
            while (!AtRear()) { Check(Time.time < deadline, "Boss rotation did not trigger rear reevaluation."); yield return null; }
            boss.transform.rotation = Quaternion.identity;
            Warp(melee, boss.transform.position + Vector3.forward * 4f);
            brain.enabled = false;
            GameObject rearWall = Wall(obstacles, boss.transform.position + Vector3.back * .35f,
                new Vector3(5f, 3f, .2f));
            Check(Find(out MeleePositionPlan side) && side.Sector == MeleeSector.Side,
                "Blocked rear did not fall back to a reachable side.");
            Warp(melee, boss.transform.position + Vector3.right * 3.5f + Vector3.forward * .8f);
            Check(Find(out MeleePositionPlan shortSide) && shortSide.Sector == MeleeSector.Side
                && shortSide.FinalDestination.x > boss.transform.position.x,
                "Asymmetric side fallback ignored the shorter safe route.");
            GameObject leftWall = Wall(obstacles, boss.transform.position + Vector3.left * .35f,
                new Vector3(.2f, 3f, 2f));
            GameObject rightWall = Wall(obstacles, boss.transform.position + Vector3.right * .35f,
                new Vector3(.2f, 3f, 2f));
            Warp(melee, boss.transform.position + Vector3.forward * 4f);
            Check(Find(out MeleePositionPlan front) && front.Sector == MeleeSector.Front,
                "Rear and side blockage did not permit a front fallback.");
            Object.Destroy(rearWall); Object.Destroy(leftWall); Object.Destroy(rightWall);
            yield return null; Physics.SyncTransforms();
            Check(Find(out MeleePositionPlan restored) && restored.Sector == MeleeSector.Rear,
                "Removing obstructions did not restore rear preference.");
            GameObject allWall = Wall(obstacles, boss.transform.position, new Vector3(7f, 3f, 7f));
            Check(!Find(out _), "Fully blocked sight falsely found a combat position.");
            Object.Destroy(allWall);
            yield return null; Physics.SyncTransforms();

            Warp(melee, boss.transform.position + Vector3.forward * 4f);
            brain.enabled = true;
            yield return Wait(.25f);
            Vector3 manualDestination = boss.transform.position + Vector3.right * 5f + Vector3.forward * 2f;
            var manualFilter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            Check(NavMesh.SamplePosition(manualDestination, out NavMeshHit manualHit, 2f, manualFilter),
                "Manual fixture destination is off NavMesh.");
            manualDestination = manualHit.position;
            MoveResult manualResult = mover.TryMove(manualDestination, MovementSource.Manual);
            Check(manualResult == MoveResult.Accepted,
                "Manual override was rejected: " + manualResult + " at " + manualDestination);
            int manualCommand = mover.CommandId;
            yield return Wait(.35f);
            Check(mover.HasManualOverride && mover.CommandId == manualCommand && brain.State == RoleState.ManualMove,
                "Rear positioning interrupted the manual order.");
            mover.Cancel();
            deadline = Time.time + 14f;
            while (!AtRear()) { Check(Time.time < deadline, "Rear positioning did not resume after manual cancellation."); yield return null; }

            brain.enabled = false;
            Warp(melee, boss.transform.position + Vector3.forward * 4f);
            object warning = new object();
            hazards.Register(warning, HazardFootprint.Rectangle(melee.transform.position - Vector3.forward * 2f,
                Vector3.forward, 4f, 4f, boundary), "T7.2 warning");
            Check(hazards.IsUnsafe(melee.transform.position), "Awareness fixture is outside its pending warning.");
            int failedRolls = 0;
            brain.SetAwarenessRollSourceForVerification(() => { failedRolls++; return 10; });
            brain.enabled = true;
            yield return Wait(.3f);
            Check(failedRolls == 1 && !brain.EscapeCommitted && brain.State != RoleState.Escaping,
                "Failed Awareness check incorrectly bypassed the ordinary positioning gate.");
            brain.enabled = false; hazards.Remove(warning);
            Warp(melee, boss.transform.position + Vector3.forward * 4f);
            warning = new object();
            hazards.Register(warning, HazardFootprint.Rectangle(melee.transform.position - Vector3.forward * 2f,
                Vector3.forward, 4f, 4f, boundary), "T7.2 warning");
            brain.SetAwarenessRollSourceForVerification(() => 1);
            brain.enabled = true;
            yield return Wait(.3f);
            Check(brain.EscapeCommitted && (brain.State == RoleState.Escaping || brain.State == RoleState.NoSafeExit),
                "Successful Awareness did not preempt rear/body positioning.");
            brain.enabled = false; hazards.Remove(warning);

            Warp(melee, boss.transform.position + Vector3.right * 5f + Vector3.forward);
            var controller = boss.GetComponent<BossBasicAttack>();
            controller.enabled = true; brain.enabled = true;
            deadline = Time.time + 3f;
            while (!(controller.CombatTarget == melee && controller.IsChasing
                && brain.State == RoleState.RetreatingToTank))
            {
                Check(Time.time < deadline, "Active chase did not prioritize Tank retreat. " + Describe());
                yield return null;
            }
            controller.enabled = false;
            brain.enabled = false;
            combat.EventResolved -= record;

            bool Find(out MeleePositionPlan plan) => MeleeDpsPositionSearch.TryFind(agent, combat, boss, tank,
                hazards, melee.MeleeRange, 60f, .35f, .15f, out plan);
            bool AtRear() => MeleeDpsPositionSearch.IsAcceptableRear(agent, combat, boss, tank,
                hazards, melee.MeleeRange, 60f, .35f, .3f, .15f) && mover.State != MovementState.Moving;
            string Describe()
            {
                Find(out MeleePositionPlan choice);
                return $"DPS={melee.transform.position}, boss={boss.transform.position}, state={brain.State}, "
                    + $"movement={mover.State}/{mover.Source}, command={mover.CommandId}, target={mover.Destination}, "
                    + $"choice={choice.Sector}/{choice.Destination}/{choice.FinalDestination}/{choice.UsesDetour}";
            }
        }

        private static GameObject Wall(Transform parent, Vector3 point, Vector3 size)
        {
            var wall = new GameObject("T7.2 temporary blocker");
            wall.transform.SetParent(parent, true);
            wall.transform.position = new Vector3(point.x, 1f, point.z);
            wall.AddComponent<BoxCollider>().size = size;
            Physics.SyncTransforms();
            return wall;
        }

        private static void Warp(CombatUnit unit, Vector3 point)
        {
            var agent = unit.GetComponent<NavMeshAgent>();
            unit.GetComponent<UnitMovement>().Cancel();
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            Check(NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, filter), "Fixture point is off NavMesh.");
            Check(agent.Warp(hit.position + Vector3.up * agent.baseOffset), "Fixture warp failed.");
            agent.ResetPath();
        }

        private static IEnumerator Wait(float duration)
        { float until = Time.time + duration; while (Time.time < until) yield return null; }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
