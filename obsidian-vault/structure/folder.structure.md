# Unity project structure

The Unity project is `raid-commander-game/`. `Assets/_Game/` is the first-party game area. Its current scaffold is intentionally lightweight; directories are added as homes for future work and do not imply that those features are implemented.

```text
raid-commander-game/
├── Assets/
│   ├── _Game/
│   │   ├── Scenes/
│   │   ├── Scripts/
│   │   │   ├── Core/
│   │   │   ├── Camera/
│   │   │   ├── Input/
│   │   │   ├── Selection/
│   │   │   ├── Movement/
│   │   │   ├── Combat/
│   │   │   ├── Units/
│   │   │   ├── Encounters/
│   │   │   ├── Abilities/
│   │   │   ├── UI/
│   │   │   ├── Content/
│   │   │   └── Modding/
│   │   ├── Data/
│   │   │   └── BaseGame/
│   │   │       ├── Units/
│   │   │       ├── Classes/
│   │   │       ├── Abilities/
│   │   │       └── Encounters/
│   │   ├── Prefabs/
│   │   ├── Input/
│   │   ├── Art/
│   │   ├── Editor/
│   │   └── Tests/
│   ├── Settings/           # Unity template render-pipeline settings
│   ├── Scenes/             # Unity template scene, pending migration
│   └── TutorialInfo/       # Unity template assets, pending cleanup
├── Packages/
└── ProjectSettings/
```

The `Scripts` folders are organized by feature. Keep components small and composable. Add subfolders or shared abstractions when real implementation needs them; the scaffold is not a commitment to build every planned system at once.

`Combat/` contains health/state, melee and immediate ranged resolution, combat events/logging, debug command production and the independent ranged tracer. `Data/BaseGame/Units/` holds stats for four party roles and the boss. Arena setup lives in `_Game/Editor/`; `RangedDpsSceneSetup` adds the fourth unit through the existing selection, movement and combat owners without regenerating arena geometry. Integration runners live in `_Game/Tests/Editor/`.

## World conventions

- This is a fully 3D Unity project. XZ is the horizontal gameplay plane; Y is elevation.
- Use 3D colliders, physics raycasts, and NavMesh navigation. Do not introduce 2D physics, tilemaps, or movement systems.
- The target camera is elevated, angled, and orthographic.

See the repository-level `AGENTS.md` for the complete architecture rules.
