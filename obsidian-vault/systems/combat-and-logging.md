# Combat and event logging v1.4

**Status:** implemented

## Overview

Combat units own health, faction, stable encounter identifiers, target references, and configurable basic combat values. `CombatSystem` validates requests, applies health changes, and publishes resolved outcomes. `CombatLogService` records outcomes for the current encounter without affecting gameplay.

Reactive presentation, boss numerical values and outlined text are documented in [Reactive health bars](health-bars.md).

## Behavior

- Party members have world-space health bars above their units. The boss has a screen-space health bar centered at the top of the screen with the name “Boss” directly below it; this HUD stays in place as the boss moves.
- The debug panel provides Tank/Melee DPS melee attacks and a Ranged DPS attack against the boss, a basic Healer-to-Tank heal, a Boss-to-Tank damage action, lethal damage to the boss, and a health/log reset covering all five units. The boss remains the sole debug attack target; no hostile selection is added. Feedback explains rejected requests.
- Melee, ranged and healing requests use XZ distance and the acting unit's configured range. Combat units turn toward their targets. Ranged attacks resolve immediately and display a brief cyan tracer; no projectile physics or line-of-sight check is performed.
- The cyan Ranged DPS starts alongside the party with 100 health, 20 damage and 18-unit range, using existing movement at speed 4.5.
- A dead unit cannot attack or heal; its movement order is cancelled, its movement executor is disabled, and its NavMeshAgent is stopped.
- The screen-space debug panel shows recent events and action feedback, and can be hidden with F8. Its buttons are also available while the panel is visible.

## Implementation

- `CombatUnit` stores faction, encounter ID, runtime health, target, and basic values. Stats supply initial maximum health; `SetMaximumHealth` preserves current health, clamps it to the cap and publishes `HealthChanged`. The component has no UI references. `CombatSceneSetup` creates and assigns per-unit `CombatStatsDefinition` ScriptableObject assets.
- `CombatSystem` validates faction, range, and alive state, applies effective health changes, then notifies C# event subscribers and its optional log service. Logging is optional and has no gameplay callbacks.
- `CombatLogEntry` is an immutable value type containing encounter-local sequence, elapsed session time, event type, stable IDs, effective amounts, optional spell ID, and flags. Runtime publishes `SwingDamage`, `RangedDamage`, `SpellHeal`, and `UnitDied`; other schema types remain future work. `RangedDamage` is appended to preserve previous enum values.
- `CombatLogService` stores up to 1,000 entries by default, removes the oldest at capacity, exposes read-only history, and resets sequence and timestamp origin for a new session.
- Party health bars are presentation-only world-space canvases. The boss health bar is a fixed screen-space HUD that subscribes to the boss unit's health changes. The debug panel resolves stable IDs from its scene unit references and formats recent entries in chronological order. Debug actions report effective results or common rejection reasons; the Boss-to-Tank action submits damage through `CombatSystem` so the heal button can be exercised without implementing autonomous boss behavior.
- `CombatSystem.TryRangedAttack` accepts distinct living opponents within the source's `RangedRange`, faces the target and uses `ApplyDamage` with `BasicDamage`. Successful effective damage publishes `RangedDamage`, followed by `UnitDied` when lethal. Invalid/dead/out-of-range requests produce neither damage nor events.
- `RangedAttackTracer` listens to `EventResolved` for its own source and displays a world-space LineRenderer for 0.2 seconds. It unsubscribes on disable and performs no combat calculations. Disabling presentation or logging does not prevent damage.
- The arena retains IDs 1–4 for Tank, Healer, DPS, and Raid Boss, and assigns ID 5 to Ranged DPS. `CombatSceneSetup.Apply` is also called by the arena generator. **Configure Ranged DPS** updates the current arena through the existing setup owners, retaining unit/asset identities and custom geometry across repeated application.

## Source files

- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatStatsDefinition.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatLogEntry.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatLogService.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatUnit.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatSystem.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatDebugActions.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/RangedAttackTracer.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/WorldHealthBar.cs`
- `raid-commander-game/Assets/_Game/Scripts/UI/CombatDebugPanel.cs`
- `raid-commander-game/Assets/_Game/Scripts/UI/ScreenBossHealthBar.cs`
- `raid-commander-game/Assets/_Game/Editor/CombatSceneSetup.cs`
- `raid-commander-game/Assets/_Game/Editor/RangedDpsSceneSetup.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/CombatVerification.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/RangedDpsVerification.cs`
- `raid-commander-game/Assets/_Game/Scenes/RaidArena.unity`
- `raid-commander-game/Assets/_Game/Data/BaseGame/Units/TankCombatStats.asset`
- `raid-commander-game/Assets/_Game/Data/BaseGame/Units/HealerCombatStats.asset`
- `raid-commander-game/Assets/_Game/Data/BaseGame/Units/DpsCombatStats.asset`
- `raid-commander-game/Assets/_Game/Data/BaseGame/Units/RangedDpsCombatStats.asset`
- `raid-commander-game/Assets/_Game/Art/Materials/RangedAttackTracer.mat`
- `raid-commander-game/Assets/_Game/Art/Materials/RangedDpsPlaceholder.mat`
- `raid-commander-game/Assets/_Game/Data/BaseGame/Units/BossCombatStats.asset`

## Current scope

Basic resolved melee/ranged damage, basic healing, health capping, death, target/faction checks, planar range, facing, health presentation, and in-memory event logging are implemented. There is no autonomous combat behavior, attack interval, right-click attack, physics projectile, line-of-sight check, automatic boss attack, armor, critical calculation, shields, periodic effects, casting, AoE, encounter result flow, or persistence. Use **Raid Commander > Configure Combat and Logging** to reapply combat setup, and **Verify Ranged DPS in Play Mode** to run the combined combat, reactive health and ranged suite. Debug buttons remain the combat entry point; press F8 to hide/show the panel.

T6 verification passed on 2026-10-20 in Unity 6000.6.6f1: immediate boundary-range and elevation-independent hits, faction/null/self/death rejection, effective lethal damage and ordered events, IDs/metadata, boss health updates, fourth-unit health/reset/death movement, tracer endpoints/expiry and independent presentation/logging. Existing melee/healing/debug checks also passed. The current Console has zero errors and compilation has no errors. Actual Game views were inspected at camera sizes 8, 20 and 30.

The boss HUD setup has been applied and saved in `RaidArena.unity` through Unity CLI and the running Editor's Pipeline connection. Live inspection confirmed a scene-root `ScreenSpaceOverlay` canvas, the label `Boss`, a reference to the boss combat unit, and removal of its world-space bar. Task T5.1 replaced legacy UI text with outlined TMP text and moved both party and boss fills onto `HealthBarBinding`. Script compilation completed without errors. `CombatVerification.Begin()` now runs the combat checks and the reactive health bar suite; both passed in Play mode.

Unity Pipeline `0.8.0-exp.1` supports this workflow from Codex CLI. In a restricted Codex command sandbox, Editor discovery and localhost access can fail even while the Editor is running. Running Unity CLI commands with approved execution outside that sandbox successfully discovers and controls this project's Editor; switching to the Codex app is unnecessary.

## Revisions

| Version | Date (YYYY-MM-DD) | Summary |
|---|---|---|
| v1.4 | 2026-10-20 | Added the fourth cyan party unit, reusable immediate ranged damage, boss-targeted debug action, resolved ranged events and independent tracer; passed runtime integration and regression checks. |
| v1.3 | 2026-10-20 | Removed UI ownership from combat units, added explicit maximum-health notifications and verified shared reactive health bars alongside existing combat behavior. |
| v1.2 | 2026-10-19 | Applied and saved the fixed boss HUD with its name below the bar; confirmed setup and passed combat verification through Unity Pipeline. |
| v1.1 | 2026-10-19 | Added debug action feedback and a boss damage action to make healing testable from the panel. |
| v1 | 2026-10-19 | Added basic combat resolution, immutable encounter log entries, bounded log history, health bars, debug actions, and a Play-mode verification runner. |
