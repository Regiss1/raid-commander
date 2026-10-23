# Development checklist

## Foundation

- [x] Create the Unity 6 project.
- [x] Configure Unity Git ignore rules.
- [x] Establish the initial `Assets/_Game/` feature-oriented folder scaffold.
- [x] Document architecture and 3D coordinate/movement conventions.
- [x] Confirm Input System setup and record the AI Navigation installation step.
- [x] Add an idempotent editor utility for the 3D arena, role placeholders, obstacles, layers, and camera controls.
- [x] Run the editor utility and save the generated arena as the first Build Settings scene.
- [x] Open the scene visually and verify camera controls and framing in Play mode.
- [x] Install AI Navigation through Unity Package Manager.
- [ ] Decide which Unity template assets and scenes to retain or replace.

## MVP

- [x] Implement an elevated, angled orthographic camera controller with ground-plane pan and smooth zoom.
- [x] Generate the 3D arena, party placeholders, boss, lighting, and obstacles in Unity.
- [x] Implement individual friendly-unit selection with 3D raycasts, role hotkeys, clear-on-empty, and an XZ selection ring.
- [x] Implement direct movement orders using 3D raycasts and NavMesh, with temporary markers and explicit command completion.
- [x] Implement four autonomous roles, Tank-first healing/self-healing, obstacle sight recovery and stationary boss attacks; verify manual overrides with continuing combat/resume, independent units and an unattended boss kill (T7; requester approved).
- [x] Keep protected boss hits/logging active at one health and add party attack-speed stats for automated attacks, preserving separate healing/boss timing and immediate debug actions; verify 0.5/1/2/7 APS and protected hit continuity (T7.1; requester approved).
- [x] Configure the arena with basic health, melee attacks, healing, and combat event logging.
- [x] Run combat verification in Play mode.
- [x] Add reactive party and boss health bars, outlined boss health values, and verify notifications and responsive layouts.
- [x] Add cyan Ranged DPS, selection hotkey 4, immediate boss-targeted debug ranged attacks, tracer feedback and reactive health; verify four-unit movement and combat regressions (T6; requester-approved).
- [x] Validate ranged/healing line of sight against obstacle footprints regardless of height, with debug-only failure feedback and verified unchanged health/events on rejection (T6.1; requester-approved).
- [x] Add mobile boss targeting/spacing, configurable per-instance abilities, clipped Wall of Flames and party escape/chase reactions with manual priority; verify runtime timing, damage/logging and existing integrations, including startup validation and stable mutual melee corrections (T8; requester approved).
- [ ] Add encounter victory/defeat and restart flow.
- [x] Add reactive selectable party frames, responsive combat HUD, live log/Editor debug swap, inert meter/ability mocks and configurable world-bar visibility ([T9](../prompts/tasks/T9-party-frames-and-responsive-combat-hud.md); requester approved).
- [x] Add party Awareness checks inside pending telegraphs, committed escape with manual-order priority, and a bounded Decisions tab beside Combat; verify deterministic timing, saved wiring, Play-mode outcomes and supported HUD layouts ([T10](../prompts/tasks/T10-awareness-and-decision-log.md); requester approved).
- [ ] Playtest for a short, replayable encounter and refine control clarity.

Do not mark an item complete from documentation alone when it describes runtime behavior; verify that behavior exists in the Unity project.
