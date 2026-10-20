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
- [ ] Implement autonomous role behavior with explicit player movement overrides.
- [x] Configure the arena with basic health, melee attacks, healing, and combat event logging.
- [x] Run combat verification in Play mode.
- [x] Add reactive party and boss health bars, outlined boss health values, and verify notifications and responsive layouts.
- [ ] Add one telegraphed boss mechanic.
- [ ] Add encounter victory/defeat and restart flow.
- [ ] Playtest for a short, replayable encounter and refine control clarity.

Do not mark an item complete from documentation alone when it describes runtime behavior; verify that behavior exists in the Unity project.
