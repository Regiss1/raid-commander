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

`Units/` contains `PartyRoleBrain`, `BossBasicAttack` and the bounded `CombatPositionSearch` helper. They own decisions independently of movement execution and combat resolution. `PartyAiSceneSetup` wires the existing arena without rebuilding geometry; full combat setup also calls this wiring. `PartyAiVerification` exercises unattended encounters and manual overrides in real Play mode. See [autonomous party AI](../systems/autonomous-party-ai.md).

Party combat stats now include validated attack speed, materialized on the four existing party assets through AI setup. Offensive scheduling belongs to `PartyRoleBrain`; debug combat entry points remain immediate. `AttackSpeedVerification` extends the combined AI runner with protected-damage continuity and player-frame cadence checks using temporary runtime stats copies.

`Abilities/` now contains configurable boss definitions, per-boss cast/readiness execution, immutable clipped ground polygons, encounter-local pending hazards and independent mesh presentation. `Encounters/ArenaBoundary` supplies the explicit playable volume. The existing boss/party producers submit navigation and combat requests for mobile targeting and reactions. `BossEncounterSceneSetup` wires the current arena, and `BossNavigationSetup` maintains the separate boss-size profile and `Scenes/RaidBossNavMesh.asset` without replacing the party bake. `Data/BaseGame/Abilities/WallOfFlames.asset` is the sole production ability. `BossEncounterVerification` covers focused runtime fixtures, production timing and original-bar combat regression. See [boss abilities and hazards](../systems/boss-abilities-and-hazards.md).

`UI/CombatHud` owns the screen-space party frames, live log, inert mock panels and Editor-only debug presentation. `UI/UiPointerBlocker` supplies current UI-hit checks to selection, movement input and camera zoom. `Input/CombatHudControls.asset` holds the V world-bar visibility action. `CombatHudSceneSetup` attaches the saved HUD, event system and binding without rerunning balance setup. See [combat HUD](../systems/combat-hud.md).

## World conventions

- This is a fully 3D Unity project. XZ is the horizontal gameplay plane; Y is elevation.
- Use 3D colliders, physics raycasts, and NavMesh navigation. Do not introduce 2D physics, tilemaps, or movement systems.
- The target camera is elevated, angled, and orthographic.

See the repository-level `AGENTS.md` for the complete architecture rules.
