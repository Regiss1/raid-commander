using System;
using System.Collections;
using System.Collections.Generic;
using RaidCommander.EditorTools;
using RaidCommander.Movement;
using RaidCommander.Selection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RaidCommander.Verification
{
    /// <summary>Exercises the saved arena in real Play mode; also callable with Unity -executeMethod.</summary>
    [InitializeOnLoad]
    public static class MovementVerification
    {
        private const string PendingKey = "RaidCommander.MovementVerification.Pending";
        private static IEnumerator checks;
        private static double startedAt;
        private static InputSettings inputSettings;
        private static InputSettings.BackgroundBehavior originalBackground;
        private static InputSettings.EditorInputBehaviorInPlayMode originalEditorInput;
        private static bool originalRunInBackground;

        static MovementVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false))
                    return;
                SessionState.SetBool(PendingKey, false);
                startedAt = EditorApplication.timeSinceStartup;
                checks = Verify();
                EditorApplication.update += Tick;
            };
        }

        [MenuItem("Raid Commander/Verify Movement in Play Mode")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run movement verification from Edit mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ArenaMovementSetup.ScenePath);
            SessionState.SetBool(PendingKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup - startedAt < 90, "Verification timed out.");
                EditorApplication.QueuePlayerLoopUpdate();
                if (!checks.MoveNext())
                    Finish(true, "all movement checks passed");
            }
            catch (Exception error)
            {
                Finish(false, error.ToString());
            }
        }

        private static void Finish(bool passed, string message)
        {
            EditorApplication.update -= Tick;
            (checks as IDisposable)?.Dispose();
            checks = null;
            if (inputSettings != null)
            {
                inputSettings.backgroundBehavior = originalBackground;
                inputSettings.editorInputBehaviorInPlayMode = originalEditorInput;
                Application.runInBackground = originalRunInBackground;
            }
            Debug.Log((passed ? "MOVEMENT VERIFY PASS: " : "MOVEMENT VERIFY FAIL: ") + message);
            if (Application.isBatchMode)
                EditorApplication.Exit(passed ? 0 : 1);
            else
                EditorApplication.isPlaying = false;
        }

        private static IEnumerator Verify()
        {
            inputSettings = InputSystem.settings;
            originalBackground = inputSettings.backgroundBehavior;
            originalEditorInput = inputSettings.editorInputBehaviorInPlayMode;
            originalRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return null;
            yield return null;
            var tank = GameObject.Find("Tank").GetComponent<UnitMovement>();
            var healer = GameObject.Find("Healer").GetComponent<UnitMovement>();
            var dps = GameObject.Find("DPS").GetComponent<UnitMovement>();
            var camera = Camera.main;
            var selection = camera.GetComponent<UnitSelectionController>();
            var input = camera.GetComponent<PlayerMovementInput>();
            var marker = GameObject.Find("Destination Marker").GetComponent<DestinationMarker>();
            UnitMovement[] party = { tank, healer, dps };
            Check(UnityEngine.Object.FindObjectsByType<UnitMovement>(FindObjectsSortMode.None).Length == 3,
                "Expected exactly three independent movers.");
            foreach (UnitMovement unit in party)
            {
                Check(unit.GetComponent<NavMeshAgent>().isOnNavMesh, unit.name + " is off the baked NavMesh.");
                Check(Mathf.Abs(unit.GetComponent<Collider>().bounds.min.y) < 0.15f, unit.name + " is not standing on the ground.");
            }

            var endings = new List<MovementState>();
            tank.CommandEnded += (id, source, state) => endings.Add(state);
            selection.Select(tank.GetComponent<SelectableUnit>());
            Vector3 target = new Vector3(-8f, 0f, -5f);

            // Exercise the actual Input System right-button binding, not just the executor API.
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                Vector2 screen = camera.WorldToScreenPoint(target);
                Physics.Raycast(camera.ScreenPointToRay(screen), out RaycastHit probe, 200f);
                Debug.Log($"INPUT CHECK: screen={screen}, viewport={camera.pixelRect}, hit={probe.collider?.name}, point={probe.point}, frame={Time.frameCount}");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Right));
                double inputDeadline = EditorApplication.timeSinceStartup + 3;
                while (tank.CommandId == 0)
                {
                    Check(EditorApplication.timeSinceStartup < inputDeadline,
                        $"Right-click Input Action did not issue movement. frame={Time.frameCount}, mouse={Mouse.current?.position.ReadValue()}, button={mouse.rightButton.isPressed}, selected={selection.SelectedUnit?.name}");
                    yield return null;
                }
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                Check(tank.HasManualOverride && marker.IsVisible && marker.WasAccepted, "Manual move/marker did not activate.");
                Check(Vector3.Distance(tank.Destination, target) < 0.2f, "Mouse ray destination is inaccurate.");
                Check(selection.SelectedUnit == tank.GetComponent<SelectableUnit>(), "Right click changed selection.");
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
            }

            Check(tank.TryMove(new Vector3(0, 0, 0), MovementSource.Autonomous) == MoveResult.ManualOverrideActive,
                "Autonomous input interrupted a manual order.");
            int previousId = tank.CommandId;
            Check(tank.TryMove(new Vector3(200, 0, 200), MovementSource.Manual) == MoveResult.InvalidDestination,
                "Off-mesh target was accepted.");
            Check(tank.CommandId == previousId && tank.HasManualOverride, "Rejected order disturbed the active order.");
            Check(tank.TryMove(new Vector3(float.NaN, 0, 0), MovementSource.Manual) == MoveResult.InvalidDestination,
                "Non-finite destination was accepted.");

            // A disconnected but valid NavMesh destination must be rejected, not followed as a partial path.
            var islandSource = new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(4, 0.2f, 4),
                transform = Matrix4x4.TRS(new Vector3(50, -0.1f, 0), Quaternion.identity, Vector3.one)
            };
            NavMeshData island = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),
                new List<NavMeshBuildSource> { islandSource }, new Bounds(new Vector3(50, 0, 0), new Vector3(8, 4, 8)),
                Vector3.zero, Quaternion.identity);
            NavMeshDataInstance islandInstance = NavMesh.AddNavMeshData(island);
            try
            {
                Check(NavMesh.SamplePosition(new Vector3(50, 0, 0), out _, 0.5f, NavMesh.AllAreas), "Test island failed to bake.");
                Check(tank.TryMove(new Vector3(50, 0, 0), MovementSource.Manual) == MoveResult.Unreachable,
                    "Disconnected destination was not rejected.");
                Check(tank.CommandId == previousId, "Unreachable command replaced the current order.");
            }
            finally
            {
                islandInstance.Remove();
                UnityEngine.Object.Destroy(island);
            }

            Vector3 cameraPosition = camera.transform.position;
            Quaternion cameraRotation = camera.transform.rotation;
            camera.transform.position = new Vector3(12, 25, -22);
            camera.transform.LookAt(Vector3.zero);
            Check(input.TryMoveAtScreenPosition(camera.WorldToScreenPoint(target)), "Angled camera rejected visible ground.");
            Check(Vector3.Distance(tank.Destination, target) < 0.2f, "Camera angle changed world destination.");
            camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            Check(endings.Contains(MovementState.Interrupted), "Replacing a valid move did not end the previous command.");

            Vector3 pillarPoint = GameObject.Find("Central Pillar").transform.position;
            previousId = tank.CommandId;
            Check(!input.TryMoveAtScreenPosition(camera.WorldToScreenPoint(pillarPoint)), "Obstacle click became a ground order.");
            Check(tank.CommandId == previousId && !marker.WasAccepted, "Obstacle rejection disturbed the current move or marker.");

            Check(tank.TryMove(new Vector3(-5, 0, 9), MovementSource.Manual) == MoveResult.Accepted, "Tank detour rejected.");
            Check(tank.GetComponent<NavMeshAgent>().path.corners.Length > 2, "Expected a detour around the central pillar.");
            Check(healer.TryMove(new Vector3(-10, 0, -10), MovementSource.Manual) == MoveResult.Accepted, "Healer move rejected.");
            Check(dps.TryMove(new Vector3(10, 0, -4), MovementSource.Manual) == MoveResult.Accepted, "DPS move rejected.");
            Vector3[] starts = { tank.transform.position, healer.transform.position, dps.transform.position };
            bool[] moved = new bool[3];
            Collider[] obstacles = GameObject.Find("Obstacles").GetComponentsInChildren<Collider>();
            double moveDeadline = EditorApplication.timeSinceStartup + 25;
            while (Array.Exists(party, unit => unit.State == MovementState.Moving))
            {
                Check(EditorApplication.timeSinceStartup < moveDeadline, "Units did not complete their moves.");
                for (int i = 0; i < party.Length; i++)
                {
                    Collider body = party[i].GetComponent<Collider>();
                    moved[i] |= Vector3.Distance(starts[i], party[i].transform.position) > 0.2f;
                    Check(Mathf.Abs(body.bounds.min.y) < 0.15f, party[i].name + " left ground elevation.");
                    foreach (Collider obstacle in obstacles)
                    {
                        bool overlap = Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                            obstacle, obstacle.transform.position, obstacle.transform.rotation, out _, out float depth);
                        Check(!overlap || depth < 0.03f, party[i].name + " passed through " + obstacle.name);
                    }
                }
                yield return null;
            }
            for (int i = 0; i < party.Length; i++)
            {
                Check(moved[i] && party[i].State == MovementState.Completed, party[i].name + " did not arrive independently.");
                Check(!party[i].HasManualOverride && !party[i].GetComponent<NavMeshAgent>().hasPath,
                    party[i].name + " retained a manual override/path after completion.");
            }
            Check(!marker.IsVisible, "Destination marker did not expire.");
            Check(endings.Contains(MovementState.Completed), "Completion event missing.");

            // Completion releases ownership so an independent producer can issue the next order.
            Check(tank.TryMove(new Vector3(-2, 0, 10), MovementSource.Autonomous) == MoveResult.Accepted,
                "Autonomous input was not allowed after completion.");
            Check(tank.TryMove(new Vector3(-3, 0, 12), MovementSource.Manual) == MoveResult.Accepted,
                "Manual input failed to interrupt autonomous movement.");
            tank.Cancel();
            Check(tank.State == MovementState.Cancelled && !tank.HasManualOverride, "Cancellation did not release override.");
            Check(tank.TryMove(tank.transform.position - Vector3.up * tank.GetComponent<NavMeshAgent>().baseOffset,
                MovementSource.Manual) == MoveResult.Accepted, "Already-arrived order rejected.");
            double arrivalDeadline = EditorApplication.timeSinceStartup + 3;
            while (tank.State == MovementState.Moving)
            {
                Check(EditorApplication.timeSinceStartup < arrivalDeadline, "Already-arrived order did not finish.");
                yield return null;
            }
            Check(tank.State == MovementState.Completed, "Already-arrived order failed.");

            tank.GetComponent<NavMeshAgent>().speed = 0f;
            Check(tank.TryMove(new Vector3(-3, 0, 12), MovementSource.Manual) == MoveResult.Accepted, "Stall check order rejected.");
            double stallDeadline = EditorApplication.timeSinceStartup + 9;
            while (tank.State == MovementState.Moving)
            {
                Check(EditorApplication.timeSinceStartup < stallDeadline, "Stalled move held override indefinitely.");
                yield return null;
            }
            Check(tank.State == MovementState.Failed && !tank.HasManualOverride, "Stalled move did not fail cleanly.");
            tank.GetComponent<NavMeshAgent>().speed = 4.5f;
            Check(tank.TryMove(new Vector3(-3, 0, 12), MovementSource.Manual) == MoveResult.Accepted, "Disable check order rejected.");
            tank.enabled = false;
            Check(tank.State == MovementState.Cancelled && !tank.HasManualOverride, "Disabling the mover did not cancel its order.");

            // Crossing movers, an idle party member, and the boss must not deflect or push each other.
            tank.enabled = true;
            int friendlyLayer = LayerMask.NameToLayer("FriendlyUnit");
            int hostileLayer = LayerMask.NameToLayer("HostileUnit");
            Check(Physics.GetIgnoreLayerCollision(friendlyLayer, friendlyLayer)
                && Physics.GetIgnoreLayerCollision(friendlyLayer, hostileLayer)
                && Physics.GetIgnoreLayerCollision(hostileLayer, hostileLayer), "Unit collision settings were not saved.");
            Vector3 bossPosition = GameObject.Find("Raid Boss").transform.position;
            Vector3 crossing = new Vector3(bossPosition.x, 0f, bossPosition.z);
            Vector3[] crossingStarts = { crossing + Vector3.left * 4, crossing, crossing + Vector3.right * 4 };
            for (int i = 0; i < party.Length; i++)
            {
                var agent = party[i].GetComponent<NavMeshAgent>();
                Check(agent.Warp(crossingStarts[i]), "Could not position unit for crossing check.");
            }
            yield return null;
            Physics.SyncTransforms();
            Vector3 idlePosition = healer.transform.position;
            // Selection still sees friendly body colliders; movement rays pass through them.
            Vector2 unitScreen = camera.WorldToScreenPoint(dps.transform.position);
            Check(Physics.Raycast(camera.ScreenPointToRay(unitScreen), out RaycastHit unitHit, 200f,
                1 << friendlyLayer) && unitHit.collider.gameObject == dps.gameObject, "Friendly selection collider is unavailable.");
            Check(input.TryMoveAtScreenPosition(unitScreen), "Movement targeting was blocked by a party body.");
            Check(input.TryMoveAtScreenPosition(camera.WorldToScreenPoint(bossPosition)), "Movement targeting was blocked by the boss.");
            tank.Cancel();
            Check(tank.TryMove(crossing + Vector3.right * 4, MovementSource.Manual) == MoveResult.Accepted,
                "Crossing tank command rejected.");
            Check(dps.TryMove(crossing + Vector3.left * 4, MovementSource.Manual) == MoveResult.Accepted,
                "Crossing DPS command rejected.");
            bool crossedBoss = false;
            double crossingDeadline = EditorApplication.timeSinceStartup + 8;
            while (tank.State == MovementState.Moving || dps.State == MovementState.Moving)
            {
                Check(EditorApplication.timeSinceStartup < crossingDeadline, "Units blocked one another during crossing.");
                Check(Mathf.Abs(tank.transform.position.z - crossing.z) < 0.05f
                    && Mathf.Abs(dps.transform.position.z - crossing.z) < 0.05f, "Units detoured around passable bodies.");
                Check(Vector3.Distance(healer.transform.position, idlePosition) < 0.01f, "Idle party member was pushed.");
                crossedBoss |= Mathf.Abs(tank.transform.position.x - crossing.x) < 0.4f;
                yield return null;
            }
            Check(crossedBoss && tank.State == MovementState.Completed && dps.State == MovementState.Completed,
                "Units did not pass through the boss and idle healer.");
            foreach (UnitMovement unit in party)
                Check(unit.TryMove(crossing, MovementSource.Manual) == MoveResult.Accepted, "Shared destination rejected.");
            double sharedDeadline = EditorApplication.timeSinceStartup + 8;
            while (Array.Exists(party, unit => unit.State == MovementState.Moving))
            {
                Check(EditorApplication.timeSinceStartup < sharedDeadline, "Overlapping destination did not complete.");
                yield return null;
            }
            foreach (UnitMovement unit in party)
            {
                Vector3 feet = unit.transform.position - Vector3.up * unit.GetComponent<NavMeshAgent>().baseOffset;
                Check(unit.State == MovementState.Completed && Vector3.Distance(feet, crossing) < 0.25f,
                    "Party member could not share the boss's ground position.");
            }
            Check(GameObject.Find("Raid Boss").transform.position == bossPosition, "Boss was pushed.");

            Debug.Log("Verified right-click input, angled raycast accuracy, independent agents, obstacle detours, rejected targets, "
                + "interruption, completion, cancellation, override release, stall failure, feet elevation, marker expiry, "
                + "pass-through crossing, shared destinations, unit targeting, and no pushing.");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
