<!-- task-tracking:start -->
**Task:** T1
**GitHub issue:** [#2](https://github.com/Regiss1/raid-commander/issues/2)
**Tracking status:** done (historical completion explicitly confirmed by requester)
**Implementation commit:** [9fd1e72](https://github.com/Regiss1/raid-commander/commit/9fd1e726136b4ae32e0920f11ae00c6772c1c380)
<!-- task-tracking:end -->

We are building a single-player, 3D, top-down MMO raid simulator in Unity 6 using C#.

Core architecture requirements:
- Fully 3D game world, NOT a 2D game.
- Unity's XZ plane represents horizontal gameplay movement; Y represents elevation.
- Elevated, angled top-down camera with orthographic projection.
- 3D models, colliders, physics raycasts, and NavMesh navigation.
- RTS-style party controls inspired by Warcraft III.
- Three autonomous units: Tank, Healer, and DPS.
- The player can select individual units and override their autonomous movement.
- Long-term support for moddable boss encounters, abilities, and classes.

TASK:
1. Inspect the existing project.
2. Establish a lightweight, feature-based folder architecture under Assets/_Game.
3. Create AGENTS.md documenting all architectural rules, especially the 3D coordinate system and movement conventions.
4. Create a README.md describing the project concept and MVP.
5. Confirm that Unity Input System and AI Navigation packages are configured or document how to install them.
6. Add suitable Git ignore rules if missing.

Do not use Rigidbody2D, Collider2D, Physics2D, 2D tilemaps, or 2D movement systems.

Prefer small, composable MonoBehaviours and straightforward C# architecture.

Do not implement gameplay yet. Summarize changed files, tests, and required editor steps.