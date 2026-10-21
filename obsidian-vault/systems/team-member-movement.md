# Team member movement v1.3

**Status:** implemented

## Overview

Each party member owns an independent NavMeshAgent and movement executor. Player input reads the selected unit and submits a world-space command. Changing selection does not stop a unit that is already moving.

## Behavior

- Select Tank, Healer, Melee DPS, or Ranged DPS, then right-click visible ground to issue a manual move.
- Camera.ScreenPointToRay and a 3D Physics.Raycast find the actual ground hit. Ground height comes from physics and the NavMesh; Y is never forced to zero.
- Agents follow paths around the arena's 3D obstacles. Selecting another unit lets the player issue independent orders while existing moves continue.
- Party members and the boss are mutually passable. Units can cross or share a destination without pushing, blocking, or detouring around one another. Right-click movement targeting passes through unit bodies to the ground; left-click selection still uses their colliders.
- A new valid manual command replaces an active move, reporting the previous command as Interrupted.
- A temporary green ring shows the accepted destination. A red ring indicates an invalid ground target or an obstacle click. Both rings lie on XZ at the destination's elevation and disappear after 1.25 seconds. Misses and clicks with no selection issue no command.
- Invalid, disconnected, and partial-path destinations are rejected before changing the active order. Walls and other solid arena obstacles still block ground targeting.

## Implementation

- `PlayerMovementInput` uses the Input System right-button action and the central selection controller. Its raycast excludes FriendlyUnit and HostileUnit, checks the first remaining collider against the Ground mask, submits a manual command, and updates `DestinationMarker`.
- `UnitMovement.TryMove(Vector3, MovementSource)` is the shared execution API. It accepts Manual or Autonomous sources without depending on selection, input devices, or role decisions.
- Destinations must be finite and sample onto the matching NavMesh within 0.5 world units. Only a complete `NavMeshAgent.CalculatePath` is accepted; `SetPath` executes that validated route. See [Unity's CalculatePath reference](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/ai/navmeshagent/calculatepath).
- `MoveResult` reports Accepted, ManualOverrideActive, AgentUnavailable, InvalidDestination, or Unreachable. Rejections do not acquire manual ownership or cancel an existing valid order. An agent failure while applying a path ends an active command as Failed.
- Every accepted command has a per-unit `CommandId`, source, and resolved `Destination`. State progresses from Idle to Moving, then Completed, Failed, or Cancelled. Replaced commands report Interrupted through `CommandEnded(id, source, state)` while the new command is Moving.
- `HasManualOverride` is true only while a manual command is Moving. Autonomous submissions are rejected during that interval. Completion, cancellation, failure, and disabling the executor release ownership. Interrupted autonomous commands are not queued or automatically resumed; their producer can evaluate a fresh order after ownership is released.
- Arrival requires proximity along the complete path or to the destination, plus a stopped agent. Arrival and cancellation reset the path. Lost/partial paths fail; lack of position progress for six seconds also fails to prevent an indefinite override.
- AI Navigation 2.0.16 supplies the arena's `NavMeshSurface`. The bake collects physics colliders on Default and Ground, excluding both unit layers. Only solid obstacle geometry is marked Not Walkable with `NavMeshModifier`; setup removes the old boss modifier before rebaking.
- The Humanoid bake profile uses radius 0.7, height 2, and step height 0.4; the surface uses 0.1-unit voxels and a height mesh. Agents retain radius 0.65 and use NoObstacleAvoidance to allow mutual overlap while the baked clearance still protects against walls. Speed is 4.5 and stopping distance is 0.15. Their base offset accounts for the centered capsule pivots so their feet and selection rings remain at ground level.
- The project's 3D physics collision matrix disables FriendlyUnit/FriendlyUnit, FriendlyUnit/HostileUnit, and HostileUnit/HostileUnit collisions. Body colliders remain enabled for raycast selection. New unit navigation must preserve this passability convention.
- `ArenaMovementSetup` updates the existing arena and saves the baked asset, preserving its GUID on rebake. The full arena generator also calls this setup.
- T6 extends the same agent/executor setup to Ranged DPS at speed 4.5. **Configure Ranged DPS** adds the fourth unit to the existing scene and rebakes its current geometry without regenerating the arena or replacing its floor edits.

## Source files

- `raid-commander-game/Assets/_Game/Scripts/Movement/UnitMovement.cs`
- `raid-commander-game/Assets/_Game/Scripts/Movement/PlayerMovementInput.cs`
- `raid-commander-game/Assets/_Game/Scripts/Movement/DestinationMarker.cs`
- `raid-commander-game/Assets/_Game/Editor/ArenaMovementSetup.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/MovementVerification.cs`
- `raid-commander-game/Assets/_Game/Editor/RangedDpsSceneSetup.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/RangedDpsVerification.cs`
- `raid-commander-game/Assets/_Game/Scenes/RaidArena.unity`
- `raid-commander-game/Assets/_Game/Scenes/RaidArenaNavMesh.asset`
- `raid-commander-game/ProjectSettings/DynamicsManager.asset`

## Current scope

V1.3 supports four party members with individual orders, static arena obstacle avoidance, mutually passable characters, manual priority, and terminal command states. T7's [autonomous party AI](autonomous-party-ai.md) now uses the existing Autonomous source; movement execution retains its API and terminal states. Brains yield positioning during manual ownership, permit valid combat while moving, and evaluate fresh destinations after release. Selection remains independent. Group formations, queued orders, runtime obstacle rebaking, and off-mesh traversal authoring are outside this version.

Navigation preserves elevation, but the current arena is flat. Multilevel targeting, slope presentation, and more complex terrain still need dedicated level validation. A shared marker shows only the most recent player request; it is feedback rather than a persistent marker for every moving unit.

After editing geometry, run **Raid Commander > Configure Movement and Bake Navigation**. The checked-in scene already references the baked NavMesh asset.

## Verification

Verified v1.2 on 2026-10-20 in Unity 6000.6.6f1. The movement suite passed with four independent movers, obstacle avoidance, crossing, shared boss destinations, and the existing interruption/failure cases. The ranged suite additionally passed synthetic right-click movement, selection switching, manual-priority rejection, arrival, and movement cancellation on death. Existing floor customization remained intact across repeated setup.

Verified v1.1 on 2026-10-18 in Unity 6000.6.6f1: the automated Play-mode checks passed, and the scene setup completed successfully. Added checks cover crossing movers through an idle teammate and the boss without displacement or detours, all three units completing at the boss's ground position, saved collision settings, retained selection colliders, and movement targeting through unit bodies. Existing static-obstacle avoidance checks also passed.

**Raid Commander > Verify Movement in Play Mode** exercises the saved scene, including synthetic Input System right-clicks, camera-angle targeting, concurrent party moves, obstacle penetration checks, invalid and disconnected targets, interruption, completion, cancellation, override release, timeout failure, feet elevation, and marker expiry. Batch runs use `-executeMethod RaidCommander.Verification.MovementVerification.Begin` without `-quit`; the verifier exits Unity with its result. Logs are written under the project's ignored `Logs/` folder.

## Revisions

| Version | Date (YYYY-MM-DD) | Summary |
|---|---|---|
| v1.3 | 2026-10-20 | Integrated independent role positioning through the existing Autonomous source; verified combat during manual movement, selection independence, ownership release and autonomous resume in T7's Play-mode suite. |
| v1.2 | 2026-10-20 | Extended setup and regression checks to four party members; verified Ranged DPS movement and preserved customized arena geometry. |
| v1.1 | 2026-10-18 | Made party members and boss mutually passable; removed unit separation, unit collisions, boss bake obstruction, and unit blocking of movement targeting. Added crossing and shared-destination checks. |
| v1 | 2026-10-18 | Added baked 3D navigation, individual right-click orders, destination feedback, unreachable-target rejection, and movement ownership with explicit command outcomes. |
