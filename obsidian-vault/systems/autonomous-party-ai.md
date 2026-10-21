# Autonomous party AI v1.0

**Status:** implemented

## Overview

Four independent party role components produce navigation and combat requests for the existing movement executor and combat resolver. A separate stationary boss producer periodically attacks the Tank. Selection does not own autonomous decisions.

## Behavior

- Tank and Melee DPS approach the boss and attack within their configured melee ranges. Ranged DPS attacks within ranged range and moves toward a preferred 12-unit distance, with a two-unit tolerance.
- Healer first heals an injured living Tank, then the other injured living party member with the lowest health percentage, including self. Ties follow assigned party order. Dead allies cannot be healed or revived.
- Healer positions at least six units away from the boss when a reachable position permits it, keeping its patient within healing range and clear sight. Without an injured ally it holds a safe position with sight/range to the living Tank, or the boss if Tank is dead.
- Blocked sight causes units to seek reachable positions around arena obstacles. AI melee also requires clear sight; trusted debug melee retains its existing behavior. Distances and sight use XZ; navigation resolves elevation from the NavMesh.
- Manual movement owns positioning temporarily. Valid attacks/heals continue while moving. Autonomous positioning resumes after completion, cancellation or failure. Selection changes do not interrupt other units.
- The boss stays stationary, attempting melee against the living Tank every two seconds. It waits when Tank is out of range or behind cover and stops when Tank dies.
- Boss death leaves party AI idle. Existing manual movement and debug health/log reset remain available. Reset revives units and re-arms decisions; it does not add an encounter-result screen.
- The F8 debug panel shows every party state/target and boss state. **Keep Boss Alive (minimum 1 HP)** defaults off. Enabled protection clamps damage to one remaining boss health, including debug kill. Events reflect effective damage, with no protected death event. Disabling protection restores lethal damage and does not revive a dead boss.

## Implementation

- `PartyRoleBrain` owns role/patient decisions using assigned encounter references rather than global target searches. Decisions run every 0.2 seconds, successful actions every 1.5 seconds, and position searches at most every 0.8 seconds per unit. Intervals, preferred spacing and safety distance are serialized and tunable.
- `CombatPositionSearch` checks 48 positions across three target-centered rings. Candidates must sample onto the matching NavMesh, be within action range, have clear sight and admit a complete path. Path length, preferred spacing and boss safety rank candidates. Failed searches report `NoReachablePosition` and retry without taking manual movement ownership.
- `UnitMovement.TryMove(..., Autonomous)` remains the path executor. Brains only cancel autonomous paths. Existing unit passability, selection colliders and navigation exclusions remain in effect.
- Actions use `CombatSystem` and its shared sight query. Self-healing uses normal range, sight, health and resolved-event checks. `SessionReset` re-arms decisions and boss timing. The combat resolver owns the optional boss damage floor so all damage producers agree.
- `PartyAiSceneSetup` updates current-arena references, preserves component identity and tuning across repeats, and saves through the Editor without regenerating geometry, overwriting health-bar settings or rebaking navigation. Full combat/arena setup calls the same role wiring.
- `CombatDebugPanel` caches brain references and only presents state. Controls, state rows and event history share a scroll view.
- T7's focused real Play-mode suite passed on 2026-10-20 in Unity 6000.6.6f1: advancing frames; unattended boss kill with all roles; boss timing/range/death; Tank-first, ally and self-healing; no revival; manual combat/resume; selection independence; melee/ranged/healing sight recovery around the saved west column; ranged retreat; unreachable-target failure; damage-floor outcomes; and reset recovery.
- The existing movement suite passed. Combined combat/health/ranged/sight checks passed with their original ten-pixel health-bar fixture applied only during Play mode; preserved requester edits set saved bars to five pixels and fail the legacy eight-to-twelve-pixel readability assertion. T7 does not change that assertion or those saved edits. Actual state/debug UI was inspected at camera sizes 8, 20 and 30. Repeated setup retained four party brains and one boss producer, and verification left the Editor in saved Edit mode.

## Source files

- `raid-commander-game/Assets/_Game/Scripts/Units/PartyRoleBrain.cs`
- `raid-commander-game/Assets/_Game/Scripts/Units/BossBasicAttack.cs`
- `raid-commander-game/Assets/_Game/Scripts/Units/CombatPositionSearch.cs`
- `raid-commander-game/Assets/_Game/Scripts/Movement/UnitMovement.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatSystem.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatDebugActions.cs`
- `raid-commander-game/Assets/_Game/Scripts/UI/CombatDebugPanel.cs`
- `raid-commander-game/Assets/_Game/Editor/PartyAiSceneSetup.cs`
- `raid-commander-game/Assets/_Game/Editor/CombatSceneSetup.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/PartyAiVerification.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/CombatVerification.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/MovementVerification.cs`
- `raid-commander-game/Assets/_Game/Scenes/RaidArena.unity`

## Current scope

One fixed encounter with four party roles and one stationary boss, baked navigation, configurable action intervals, obstacle sight recovery, manual positioning priority, and debug state/floor controls. The saved arena supports unattended boss defeat. The damage-floor toggle lasts for the Play session and remains enabled across health/log reset until toggled off or Play mode ends.

Complex boss mechanics, threat/target switching, advanced formations, manual spells, casts/projectiles, victory/defeat screens, a new restart flow and dynamic navigation rebaking are excluded. Bounded position sampling does not guarantee solutions for arbitrary layouts or prove that no firing position exists. Existing combat ranges/stats remain authoritative.

Run **Raid Commander > Configure Autonomous Party AI** for AI-only current-scene wiring. **Verify Party AI in Play Mode** runs acceptance checks from Edit mode, exits Play mode and writes `Logs/t7-ai-verification.txt`. Existing combat/movement runners suspend autonomy within their temporary Play sessions so isolated regression fixtures retain their original scope.

## Revisions

| Version | Date (YYYY-MM-DD) | Summary |
|---|---|---|
| v1.0 | 2026-10-20 | Added four role brains, bounded navigation/sight positioning, stationary boss attacks, manual priority with continuing combat, self-healing, state debugging and the boss damage floor; passed focused Play-mode integration checks. |
