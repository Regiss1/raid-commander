# Development checklist

## Foundation

- [x] Create the Unity 6 project.
- [x] Configure Unity Git ignore rules.
- [x] Establish the initial `Assets/_Game/` feature-oriented folder scaffold.
- [x] Document architecture and 3D coordinate/movement conventions.
- [x] Confirm Input System setup and record the AI Navigation installation step.
- [ ] Install AI Navigation through Unity Package Manager.
- [ ] Decide which Unity template assets and scenes to retain or replace.

## MVP

- [ ] Create a 3D arena and an elevated, angled orthographic camera.
- [ ] Add three placeholder party units: Tank, Healer, and DPS, plus one boss.
- [ ] Implement single-unit selection and direct movement orders using 3D raycasts and NavMesh.
- [ ] Implement autonomous role behavior with explicit player movement overrides.
- [ ] Add basic health, damage, attacks, healing, and one telegraphed boss mechanic.
- [ ] Add encounter victory/defeat and restart flow.
- [ ] Playtest for a short, replayable encounter and refine control clarity.

Do not mark an item complete from documentation alone when it describes runtime behavior; verify that behavior exists in the Unity project.
