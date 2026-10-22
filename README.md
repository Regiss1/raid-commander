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

For the command API, completion states, and current limits, see [team member movement v1.3](obsidian-vault/systems/team-member-movement.md). Run **Raid Commander > Verify Movement in Play Mode** from Edit mode to repeat the four-unit movement checks; the verification exits Play mode when finished.

All four party members act automatically: Tank and Melee DPS approach and attack, Ranged DPS maintains attack spacing, and Healer prioritizes the injured Tank before other injured allies, including self. The mobile boss initially chases the closest unit, locks onto Tank after positive Tank damage, and swings every three seconds within range/sight. It maintains melee spacing and retreats at half speed while facing its target. Units move around obstacles for clear sight. Manual movement overrides positioning while valid attacks/heals continue; autonomous positioning resumes afterward. Boss death leaves party AI idle. See [autonomous party AI](obsidian-vault/systems/autonomous-party-ai.md).

The combat HUD shows four clickable, reactive party frames on the right, a live scrolling combat log on the left, and the existing boss health at the top. Clicking a frame selects that unit; world selection and hotkeys update the same highlight. **V** hides/shows only the four world-space party bars, using an Input System action in `Assets/_Game/Input/CombatHudControls.asset`. The meter and group-ability panels are labeled mocks. See [Combat HUD](obsidian-vault/systems/combat-hud.md).

In the Editor, **Raid Commander > UI > Show Combat Debug** swaps the log for scrollable debug controls and history. It starts off each Editor session; F8 no longer toggles the panel. The debug view shows unit AI state and target. **Keep Boss Alive (minimum 1 HP)** holds boss health at one while attacks continue reporting normal damage and ranged tracers. Party attack-speed stats default to **1 attack per second** and preserve authored balance values; automated Tank/Melee/Ranged attacks use these stats. Healer healing stays at 1.5 seconds and boss attacks at three seconds. Debug attack/heal buttons remain immediate. **Ranged DPS -> Boss** produces an immediate attack and brief cyan tracer. Right-click attacks remain future work.

Use **Raid Commander > Configure Autonomous Party AI** to update encounter wiring in the open arena without regenerating geometry. It creates the boss navigation bake if missing and preserves both existing bakes on repeat. The saved scene is configured. **Verify Party AI in Play Mode** exercises an unattended encounter, role priorities, obstacle routing, manual combat/resume, boss timing, death/reset and the debug damage floor. The existing movement and combat suites suspend AI only for their temporary regression fixtures.

**Wall of Flames** starts 90 seconds after Play activation/reset, then every 90 seconds between starts. The boss locks a three-second cast aimed at a random living party member. Escape its yellow four-unit-wide footprint before it flashes red and deals one 95-damage hit. The footprint ends at the explicit arena boundary and passes through internal obstacles. Every role attempts to escape; non-Tanks being actively chased otherwise retreat toward the living Tank. Manual orders override both reactions, including orders into danger. See [boss abilities and hazards](obsidian-vault/systems/boss-abilities-and-hazards.md).

**Configure Boss Encounter and Wall of Flames** reapplies the saved encounter while preserving tuning and geometry. After geometry edits, **Configure Movement and Bake Navigation** rebakes the separate party and boss surfaces. The boss's larger body has its own clearance profile; unit bodies remain mutually passable. **Verify Boss Encounter in Play Mode** runs focused fixtures; **Verify Boss Production Timing in Play Mode** runs the real 90-second delay and captures warning/impact views. **Verify Combat Regression with Original Bar Fixture** runs existing combat/health/ranged/sight checks with temporary ten-pixel bars, preserving the saved five-pixel customization.

**Verify Protected Combat and Attack Speed in Play Mode** runs the combined AI suite with repeated protected hits, positive damage logs/tracer feedback, configurable attack-rate checks, unchanged healing/boss intervals and immediate debug actions. Attack speed is configured on the existing party combat stats assets; applying AI setup preserves valid custom values.

Ranged attacks and targeted heals fail when a solid collider under **Obstacles** crosses their XZ path; low and high cover block equally. The debug panel explains the line-of-sight failure without changing health or adding a combat-log entry. Unit bodies and ground do not block these actions. Use **Raid Commander > Verify Combat Line of Sight in Play Mode** to repeat the integrated checks.

**Raid Commander > Configure Ranged DPS** updates the open arena with all four party units and their selection, movement, combat and health wiring while preserving its geometry. The saved scene is already configured. **Verify Ranged DPS in Play Mode** runs the combined combat, reactive health and ranged checks. See [combat and event logging](obsidian-vault/systems/combat-and-logging.md).
