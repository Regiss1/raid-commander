# Combat and event logging v1.3

**Status:** implemented

## Overview

Combat units own health, faction, stable encounter identifiers, target references, and configurable basic combat values. `CombatSystem` validates requests, applies health changes, and publishes resolved outcomes. `CombatLogService` records outcomes for the current encounter without affecting gameplay.

Reactive presentation, boss numerical values and outlined text are documented in [Reactive health bars](health-bars.md).

## Behavior

- Party members have world-space health bars above their units. The boss has a screen-space health bar centered at the top of the screen with the name “Boss” directly below it; this HUD stays in place as the boss moves.
- The debug panel provides melee actions from Tank or DPS to the boss, a basic Healer-to-Tank heal, a Boss-to-Tank damage action, lethal damage to the boss, and a health/log reset. It reports resolved effects and explains common no-effect cases such as out-of-range attacks or healing a full-health target.
- Melee and healing requests use XZ distance and the acting unit's configured range. Combat units turn toward their targets.
- A dead unit cannot attack or heal; its movement order is cancelled, its movement executor is disabled, and its NavMeshAgent is stopped.
- The screen-space debug panel shows recent events and action feedback, and can be hidden with F8. Its buttons are also available while the panel is visible.

## Implementation

- `CombatUnit` stores faction, encounter ID, runtime health, target, and basic values. Stats supply initial maximum health; `SetMaximumHealth` preserves current health, clamps it to the cap and publishes `HealthChanged`. The component has no UI references. `CombatSceneSetup` creates and assigns per-unit `CombatStatsDefinition` ScriptableObject assets.
- `CombatSystem` validates faction, range, and alive state, applies effective health changes, then notifies C# event subscribers and its optional log service. Logging is optional and has no gameplay callbacks.
- `CombatLogEntry` is an immutable value type containing encounter-local sequence, elapsed session time, event type, stable IDs, effective amounts, optional spell ID, and flags. The schema includes future event types, while runtime publishes only `SwingDamage`, `SpellHeal`, and `UnitDied` so far.
- `CombatLogService` stores up to 1,000 entries by default, removes the oldest at capacity, exposes read-only history, and resets sequence and timestamp origin for a new session.
- Party health bars are presentation-only world-space canvases. The boss health bar is a fixed screen-space HUD that subscribes to the boss unit's health changes. The debug panel resolves stable IDs from its scene unit references and formats recent entries in chronological order. Debug actions report effective results or common rejection reasons; the Boss-to-Tank action submits damage through `CombatSystem` so the heal button can be exercised without implementing autonomous boss behavior.
- The generated arena assigns IDs 1–4 to Tank, Healer, DPS, and Raid Boss. `CombatSceneSetup.Apply` is also called by the arena generator.

## Source files

- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatStatsDefinition.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatLogEntry.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatLogService.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatUnit.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatSystem.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatDebugActions.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/WorldHealthBar.cs`
- `raid-commander-game/Assets/_Game/Scripts/UI/CombatDebugPanel.cs`
- `raid-commander-game/Assets/_Game/Scripts/UI/ScreenBossHealthBar.cs`
- `raid-commander-game/Assets/_Game/Editor/CombatSceneSetup.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/CombatVerification.cs`
- `raid-commander-game/Assets/_Game/Scenes/RaidArena.unity`
- `raid-commander-game/Assets/_Game/Data/BaseGame/Units/TankCombatStats.asset`
- `raid-commander-game/Assets/_Game/Data/BaseGame/Units/HealerCombatStats.asset`
- `raid-commander-game/Assets/_Game/Data/BaseGame/Units/DpsCombatStats.asset`
- `raid-commander-game/Assets/_Game/Data/BaseGame/Units/BossCombatStats.asset`

## Current scope

Basic resolved melee damage, basic healing, health capping, death, target/faction checks, planar range, facing, health presentation, and in-memory event logging are implemented. There is no autonomous combat behavior, boss attack, armor, critical calculation, shields, periodic effects, casting, AoE, encounter result flow, or persistence. To configure a generated arena, use **Raid Commander > Configure Combat and Logging**. To verify combat behavior, use **Raid Commander > Verify Combat in Play Mode**. In Play mode, click the debug panel buttons; use **Damage Tank (Boss)** before **Heal: Healer to Tank** to see effective healing; press F8 to hide or show the panel.

The boss HUD setup has been applied and saved in `RaidArena.unity` through Unity CLI and the running Editor's Pipeline connection. Live inspection confirmed a scene-root `ScreenSpaceOverlay` canvas, the label `Boss`, a reference to the boss combat unit, and removal of its world-space bar. Task T5.1 replaced legacy UI text with outlined TMP text and moved both party and boss fills onto `HealthBarBinding`. Script compilation completed without errors. `CombatVerification.Begin()` now runs the combat checks and the reactive health bar suite; both passed in Play mode.

Unity Pipeline `0.8.0-exp.1` supports this workflow from Codex CLI. In a restricted Codex command sandbox, Editor discovery and localhost access can fail even while the Editor is running. Running Unity CLI commands with approved execution outside that sandbox successfully discovers and controls this project's Editor; switching to the Codex app is unnecessary.

## Revisions

| Version | Date (YYYY-MM-DD) | Summary |
|---|---|---|
| v1.3 | 2026-10-20 | Removed UI ownership from combat units, added explicit maximum-health notifications and verified shared reactive health bars alongside existing combat behavior. |
| v1.2 | 2026-10-19 | Applied and saved the fixed boss HUD with its name below the bar; confirmed setup and passed combat verification through Unity Pipeline. |
| v1.1 | 2026-10-19 | Added debug action feedback and a boss damage action to make healing testable from the panel. |
| v1 | 2026-10-19 | Added basic combat resolution, immutable encounter log entries, bounded log history, health bars, debug actions, and a Play-mode verification runner. |
