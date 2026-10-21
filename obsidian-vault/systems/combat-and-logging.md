# Combat and event logging v1.8

**Status:** implemented

## Overview

Combat units own health, faction, stable encounter identifiers, target references, and configurable basic combat values. `CombatSystem` validates requests, applies health changes, and publishes resolved outcomes. `CombatLogService` records outcomes for the current encounter without affecting gameplay.

Reactive presentation, boss numerical values and outlined text are documented in [Reactive health bars](health-bars.md).

## Behavior

- Party members have world-space health bars above their units. The boss has a screen-space health bar centered at the top of the screen with the name “Boss” directly below it; this HUD stays in place as the boss moves.
- The debug panel provides Tank/Melee DPS melee attacks and a Ranged DPS attack against the boss, a basic Healer-to-Tank heal, a Boss-to-Tank damage action, lethal damage to the boss, and a health/log reset covering all five units. The boss remains the sole debug attack target; no hostile selection is added. Feedback explains rejected requests.
- Melee, ranged and healing requests use XZ distance and the acting unit's configured range. Successful combat actions turn toward their targets. Ranged attacks resolve immediately and display a brief cyan tracer; no projectile physics is performed.
- Ranged attacks and targeted heals require a clear XZ path through the configured `Obstacles` hierarchy. Any active, enabled, non-trigger collider in that hierarchy blocks regardless of its height or the units' elevation. Bodies, ground and unrelated scene colliders do not block sight.
- Blocked debug actions show `Ranged DPS failed because of line of sight` or `Healer failed because of line of sight`. They change no health, append no combat history, emit no success/death event and create no tracer. Melee and direct damage/healing/debug kill remain independent of sight.
- The cyan Ranged DPS starts alongside the party with 100 health, 20 damage and 18-unit range, using existing movement at speed 4.5.
- A dead unit cannot attack or heal; its movement order is cancelled, its movement executor is disabled, and its NavMeshAgent is stopped.
- The screen-space debug panel shows recent events and action feedback, and can be hidden with F8. Its buttons are also available while the panel is visible.

## Implementation

- T8's [boss abilities](boss-abilities-and-hazards.md) publish existing `SpellCastStart`, `SpellCastSuccess`, `SpellCastFailed` and `SpellDamage` through the same resolver/log flow. `PublishCast` restricts lifecycle types; `ApplyDamage` accepts an optional spell ID and preserves it on lethal death events. Successful zero-hit ground casts still resolve success, while cancelled casts cause no damage. `CombatUnit.FacingLocked` prevents combat-facing helpers from turning a locked caster. The debug history shows spell IDs. Logging and telegraph rendering remain independent of damage.

- [Autonomous party AI](autonomous-party-ai.md) produces periodic melee/ranged/healing requests, including Tank-first healing and the boss attack producer extended by T8. `HasLineOfSight` exposes the existing sight helper to these producers. `TryHeal` permits self-healing while attack source/target restrictions remain unchanged. `SessionReset` notifies brains after health/log reset.
- The debug panel shows each unit's AI state/target. **Keep Boss Alive (minimum 1 HP)** defaults off. `ApplyDamage` separates reported damage from health loss for the protected debug-boss target: a valid hit reports its full requested damage while resulting boss health stays at least one, including repeated hits at one health and debug kill. Protected hits cannot publish a death event. Without protection, reported damage remains effective health loss and lethal hits retain normal damage/death ordering. The toggle persists across health/log reset, does not revive a dead boss, and resets when Play mode ends.
- `TryMeleeAttack` and `TryRangedAttack` retain their existing call forms and add reported-damage output overloads. Debug feedback uses the resolved amount instead of subtracting boss health, so protected hits report normal damage and protected debug kill explains the surviving boss. Successful protected ranged outcomes continue driving the existing independent tracer. No artificial health-change event is emitted when a protected hit causes zero health loss.
- Party stats and `CombatUnit` expose attacks per second, defaulting to 1 with a statless fallback. Nonpositive/nonfinite values fall back to 1 and tiny positives clamp to 0.01. Only party automation schedules attacks from this value; normal debug combat calls remain immediate. Healer healing and boss timing retain separate intervals, with T8 setting boss swings to three seconds. See the dedicated [AI note](autonomous-party-ai.md).

- `CombatUnit` stores faction, encounter ID, runtime health, target, and basic values. Stats supply initial maximum health; `SetMaximumHealth` preserves current health, clamps it to the cap and publishes `HealthChanged`. The component has no UI references. `CombatSceneSetup` creates and assigns per-unit `CombatStatsDefinition` ScriptableObject assets.
- `CombatSystem` validates faction, range, and alive state, applies health changes, then notifies C# event subscribers and its optional log service. Damage amounts are effective health loss in ordinary combat and normal hit damage while debug boss protection is active. Logging is optional and has no gameplay callbacks.
- `CombatLogEntry` is an immutable value type containing encounter-local sequence, elapsed session time, event type, stable IDs, resolved amounts, optional spell ID, and flags. Damage retains the documented protected-boss exception; healing remains effective restored health. Runtime publishes basic damage/healing/death and T8's cast lifecycle/spell damage; other schema types remain future work. `RangedDamage` is appended to preserve previous enum values.
- `CombatLogService` stores up to 1,000 entries by default, removes the oldest at capacity, exposes read-only history, and resets sequence and timestamp origin for a new session.
- Party health bars are presentation-only world-space canvases. The boss health bar is a fixed screen-space HUD that subscribes to the boss unit's health changes. The debug panel resolves stable IDs from its scene unit references and formats recent entries in chronological order. Debug actions report effective results or common rejection reasons; the Boss-to-Tank action submits damage through `CombatSystem` so the heal button can be exercised without implementing autonomous boss behavior.
- `CombatSystem.TryRangedAttack` accepts distinct living opponents within the source's `RangedRange` and with clear sight, faces the target and uses `ApplyDamage` with `BasicDamage`. Successful resolved damage publishes `RangedDamage`, followed by `UnitDied` when lethal. Protected boss hits retain normal damage/tracer outcomes without death. Invalid/dead/out-of-range/obstructed requests produce neither damage nor events.
- `CombatLineOfSight` is a reusable helper owned by `CombatSystem`, not a scene manager or UI component. It gathers current active solid colliders under the serialized obstacle-root reference and checks an initial vertical-capsule overlap followed by a vertical-capsule sweep along the source-to-target XZ segment. Its vertical extent covers all qualifying obstacle bounds, making the query independent of collider/unit elevation while preserving actual collider shapes. A positive 0.001-unit radius provides a small PhysX query tolerance; no collider is moved or resized. Transforms synchronize before each action query, supporting moved/reparented/disabled obstacles. Empty or unassigned roots contain no blockers; arena setup explicitly requires and assigns the existing `Obstacles` hierarchy.
- `TryRangedAttack` and `TryHeal` retain their original boolean overloads and add an `out CombatActionFailure` overload. Existing source/target/faction/range validation takes priority; sight rejection reports `LineOfSight` before resolution. The debug producer uses that result for feedback without recalculating geometry. Failed requests are not added to `CombatLogEntry` or `CombatEventType`.
- `RangedAttackTracer` listens to `EventResolved` for its own source and displays a world-space LineRenderer for 0.2 seconds. It unsubscribes on disable and performs no combat calculations. Disabling presentation or logging does not prevent damage.
- The arena retains IDs 1–4 for Tank, Healer, DPS, and Raid Boss, and assigns ID 5 to Ranged DPS. `CombatSceneSetup.Apply` is also called by the arena generator. **Configure Ranged DPS** updates the current arena through the existing setup owners, retaining unit/asset identities and custom geometry across repeated application.

## Source files

- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatStatsDefinition.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatLogEntry.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatLogService.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatUnit.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatSystem.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatLineOfSight.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatDebugActions.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/RangedAttackTracer.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/WorldHealthBar.cs`
- `raid-commander-game/Assets/_Game/Scripts/UI/CombatDebugPanel.cs`
- `raid-commander-game/Assets/_Game/Scripts/UI/ScreenBossHealthBar.cs`
- `raid-commander-game/Assets/_Game/Editor/CombatSceneSetup.cs`
- `raid-commander-game/Assets/_Game/Editor/RangedDpsSceneSetup.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/CombatVerification.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/RangedDpsVerification.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/LineOfSightVerification.cs`
- `raid-commander-game/Assets/_Game/Scenes/RaidArena.unity`
- `raid-commander-game/Assets/_Game/Data/BaseGame/Units/TankCombatStats.asset`
- `raid-commander-game/Assets/_Game/Data/BaseGame/Units/HealerCombatStats.asset`
- `raid-commander-game/Assets/_Game/Data/BaseGame/Units/DpsCombatStats.asset`
- `raid-commander-game/Assets/_Game/Data/BaseGame/Units/RangedDpsCombatStats.asset`
- `raid-commander-game/Assets/_Game/Art/Materials/RangedAttackTracer.mat`
- `raid-commander-game/Assets/_Game/Art/Materials/RangedDpsPlaceholder.mat`
- `raid-commander-game/Assets/_Game/Data/BaseGame/Units/BossCombatStats.asset`

## Current scope

Basic resolved melee/ranged damage, targeted/self healing, obstacle-footprint sight checks, health capping, death, target/faction checks, planar range, facing, health presentation, and in-memory event logging are implemented. T8 adds bounded casts/ground AoE and spell identity through the existing pipeline; see the boss note. There is no right-click attack, physics projectile, height-aware/partial cover, armor, critical calculation, shields, periodic effects, encounter result flow, or persistence. Use **Raid Commander > Configure Combat and Logging** for full combat setup, **Configure Autonomous Party AI** for current encounter wiring, and **Verify Combat Line of Sight in Play Mode** for the combined combat, reactive health, ranged and sight suite. Press F8 to hide/show the panel.

T6.1 runtime checks passed on 2026-10-20 for blocked and clear ranged attacks/heals, low/high/elevated obstacles, nested hierarchy filtering, disabled/inactive/trigger colliders, unrelated Default-layer geometry, segment endpoints, rotated collider footprints, initial overlaps and the existing arena column. Rejection preserves health/bar fill, events and log history; clear paths retain normal effective outcomes and tracer feedback. Melee and trusted direct damage/healing/kill/reset checks passed. The existing combat, reactive-health and T6 ranged checks also passed. The requester approved both T6 and T6.1 on 2026-10-20; separate task commits and GitHub issue accounting retain their review boundaries.

T6 verification passed on 2026-10-20 in Unity 6000.6.6f1: immediate boundary-range and elevation-independent hits, faction/null/self/death rejection, effective lethal damage and ordered events, IDs/metadata, boss health updates, fourth-unit health/reset/death movement, tracer endpoints/expiry and independent presentation/logging. Existing melee/healing/debug checks also passed. The current Console has zero errors and compilation has no errors. Actual Game views were inspected at camera sizes 8, 20 and 30.

The boss HUD setup has been applied and saved in `RaidArena.unity` through Unity CLI and the running Editor's Pipeline connection. Live inspection confirmed a scene-root `ScreenSpaceOverlay` canvas, the label `Boss`, a reference to the boss combat unit, and removal of its world-space bar. Task T5.1 replaced legacy UI text with outlined TMP text and moved both party and boss fills onto `HealthBarBinding`. Script compilation completed without errors. `CombatVerification.Begin()` now runs the combat checks and the reactive health bar suite; both passed in Play mode.

Unity Pipeline `0.8.0-exp.1` supports this workflow from Codex CLI. In a restricted Codex command sandbox, Editor discovery and localhost access can fail even while the Editor is running. Running Unity CLI commands with approved execution outside that sandbox successfully discovers and controls this project's Editor; switching to the Codex app is unnecessary.

## Revisions

| Version | Date (YYYY-MM-DD) | Summary |
|---|---|---|
| v1.8 | 2026-10-21 | Activated cast lifecycle/spell damage through the central resolver, propagated spell identity to lethal outcomes, protected locked caster facing and displayed spell IDs; retained normal/protected damage semantics. |
| v1.7 | 2026-10-20 | Separated protected hit damage from health loss so damage events/logs/tracers continue at one boss health; exposed resolved amounts for accurate debug feedback and added validated party attack-speed stats/fallbacks. |
| v1.6 | 2026-10-20 | Integrated autonomous role/boss producers, normal self-healing, shared sight access and reset notifications; added effective boss damage-floor resolution and per-unit state debugging. |
| v1.5 | 2026-10-20 | Added shared height-independent obstacle sight checks for ranged attacks and targeted healing, with debug-only rejection reasons; verified collider filtering, clear recovery and combat regressions. |
| v1.4 | 2026-10-20 | Added the fourth cyan party unit, reusable immediate ranged damage, boss-targeted debug action, resolved ranged events and independent tracer; passed runtime integration and regression checks. |
| v1.3 | 2026-10-20 | Removed UI ownership from combat units, added explicit maximum-health notifications and verified shared reactive health bars alongside existing combat behavior. |
| v1.2 | 2026-10-19 | Applied and saved the fixed boss HUD with its name below the bar; confirmed setup and passed combat verification through Unity Pipeline. |
| v1.1 | 2026-10-19 | Added debug action feedback and a boss damage action to make healing testable from the panel. |
| v1 | 2026-10-19 | Added basic combat resolution, immutable encounter log entries, bounded log history, health bars, debug actions, and a Play-mode verification runner. |
