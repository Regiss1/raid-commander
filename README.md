# Raid Commander

Raid Commander is a single-player, 3D top-down raid simulator built with Unity 6 and C#. The player commands a small MMO-style party using RTS controls while each member handles its role autonomously. The player can select units and take direct control of their positioning when an encounter calls for it.

## MVP

The first playable encounter is intentionally small:

- One arena and one boss encounter.
- Four party units: Tank, Healer, Melee DPS, and Ranged DPS.
- Autonomous role behavior, with player selection and movement orders that override autonomous movement.
- A single boss mechanic, basic combat, and clear victory/defeat outcomes.
- Placeholder 3D models are sufficient for the first iteration.

Networking, progression systems, multiple dungeons, elaborate art, and mod authoring tools are outside the MVP. The architecture should leave room for data-driven classes, abilities, and encounters over time.

## 3D conventions

The world is fully 3D. Gameplay movement is on the XZ plane; Y is elevation. Use 3D colliders, physics raycasts, and NavMesh navigation. The camera is an angled, elevated orthographic camera. See [AGENTS.md](AGENTS.md) for project architecture rules.

## Task workflow

Game development tasks use [task files and GitHub Issues](obsidian-vault/structure/task-workflow.md). Implementations remain uncommitted for review; an explicit task-specific approval authorizes commit, push, and issue completion. GitHub Actions validates task filenames and exposes the issue lifecycle once the workflow is published to `main`.

Use the development skill's `revise T5` operation to clarify and record a scope change without implementing it. Scope revisions become separate dotted tasks such as `T5.2`, with their own issue and approval; an unmet original acceptance criterion stays under the existing open task.

## Unity setup

Open `raid-commander-game/` as the Unity project. It targets Unity `6000.6.6f1`.

- **Input System:** `com.unity.inputsystem` 1.20.0 is in `Packages/manifest.json`; Player Settings currently select the new Input System.
- **AI Navigation:** `com.unity.ai.navigation` 2.0.16 is installed. The arena includes a baked NavMeshSurface and a NavMeshAgent on each party member.

Open `Assets/_Game/Scenes/RaidArena.unity` to try camera controls, individual selection, and party movement.

## Generate the playable test arena

After Unity imports the project scripts, run **Raid Commander > Generate Playable Arena** from the editor menu. This idempotently creates or refreshes `Assets/_Game/Scenes/RaidArena.unity`, its shared 3D materials, the Ground/FriendlyUnit/HostileUnit layers, and the first enabled Build Settings scene. Open that scene and press Play.

Use **WASD** or the arrow keys to pan over the ground plane and the **mouse wheel** to zoom. Left-click a friendly unit to select it, or press **1** for Tank, **2** for Healer, **3** for Melee DPS, and **4** for the cyan Ranged DPS. Clicking elsewhere clears selection. Camera panning is smoothed and follows horizontal ground directions; zoom is orthographic and adjustable in the camera component.

**Right-click visible ground** to move the selected unit along the NavMesh. Each unit continues its own move when you change selection. A new valid order interrupts the current order. A green destination ring confirms an accepted order; a red ring marks a rejected ground or obstacle click. Markers disappear after 1.25 seconds. Rejected destinations leave the current move intact.

After editing arena geometry, use **Raid Commander > Configure Movement and Bake Navigation** to rebake the existing scene. **Generate Playable Arena** rebuilds the arena from its defaults and includes the movement setup. The checked-in scene and NavMesh are ready to play without either step.

Party members and the boss can pass through each other and share destinations. Movement clicks pass through their bodies to the ground; solid arena obstacles still block paths.

For the command API, completion states, and current limits, see [team member movement v1.2](obsidian-vault/systems/team-member-movement.md). Run **Raid Commander > Verify Movement in Play Mode** from Edit mode to repeat the four-unit movement checks; the verification exits Play mode when finished.

Use **Ranged: Ranged DPS to Boss** in the combat debug panel for an immediate ranged attack and brief cyan tracer. The boss is the debug attack target; party selection does not change it. Ranged DPS starts with 100 health, 20 damage and an 18-unit attack range. Existing melee, healing, damage and reset buttons remain available; **F8** hides/shows the panel. Combat is still triggered through debug buttons; autonomous attacks and right-click attacks are future work.

Ranged attacks and targeted heals fail when a solid collider under **Obstacles** crosses their XZ path; low and high cover block equally. The debug panel explains the line-of-sight failure without changing health or adding a combat-log entry. Unit bodies and ground do not block these actions. Use **Raid Commander > Verify Combat Line of Sight in Play Mode** to repeat the integrated checks.

**Raid Commander > Configure Ranged DPS** updates the open arena with all four party units and their selection, movement, combat and health wiring while preserving its geometry. The saved scene is already configured. **Verify Ranged DPS in Play Mode** runs the combined combat, reactive health and ranged checks. See [combat and event logging](obsidian-vault/systems/combat-and-logging.md).
