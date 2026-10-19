# Raid Commander

Raid Commander is a single-player, 3D top-down raid simulator built with Unity 6 and C#. The player commands a small MMO-style party using RTS controls while each member handles its role autonomously. The player can select units and take direct control of their positioning when an encounter calls for it.

## MVP

The first playable encounter is intentionally small:

- One arena and one boss encounter.
- Three party units: Tank, Healer, and DPS.
- Autonomous role behavior, with player selection and movement orders that override autonomous movement.
- A single boss mechanic, basic combat, and clear victory/defeat outcomes.
- Placeholder 3D models are sufficient for the first iteration.

Networking, progression systems, multiple dungeons, elaborate art, and mod authoring tools are outside the MVP. The architecture should leave room for data-driven classes, abilities, and encounters over time.

## 3D conventions

The world is fully 3D. Gameplay movement is on the XZ plane; Y is elevation. Use 3D colliders, physics raycasts, and NavMesh navigation. The camera is an angled, elevated orthographic camera. See [AGENTS.md](AGENTS.md) for project architecture rules.

## Unity setup

Open `raid-commander-game/` as the Unity project. It targets Unity `6000.6.6f1`.

- **Input System:** `com.unity.inputsystem` 1.20.0 is in `Packages/manifest.json`; Player Settings currently select the new Input System.
- **AI Navigation:** Not installed yet. In Unity, open **Window > Package Management > Package Manager**, choose **Unity Registry**, search for **AI Navigation** (`com.unity.ai.navigation`), and install a version compatible with the project editor. This supplies NavMesh authoring and runtime navigation components.

The project currently contains Unity's starter scene and template assets. No gameplay has been implemented.
