# Autonomous party AI v1.2

**Status:** implemented

## Overview

Four independent party role components produce navigation and combat requests for the existing movement executor and combat resolver. The mobile boss and ability runner are documented in [Boss abilities and ground hazards](boss-abilities-and-hazards.md). Selection does not own autonomous decisions.

## Behavior

- Tank and Melee DPS approach the boss and attack within their configured melee ranges. Ranged DPS attacks within ranged range and moves toward a preferred 12-unit distance, with a two-unit tolerance.
- Healer first heals an injured living Tank, then the other injured living party member with the lowest health percentage, including self. Ties follow assigned party order. Dead allies cannot be healed or revived.
- Healer positions at least six units away from the boss when a reachable position permits it, keeping its patient within healing range and clear sight. Without an injured ally it holds a safe position with sight/range to the living Tank, or the boss if Tank is dead.
- Blocked sight causes units to seek reachable positions around arena obstacles. AI melee also requires clear sight; trusted debug melee retains its existing behavior. Distances and sight use XZ; navigation resolves elevation from the NavMesh.
- Manual movement owns positioning temporarily. Valid attacks/heals continue while moving. Autonomous positioning resumes after completion, cancellation or failure. Selection changes do not interrupt other units.
- T8 adds pending-hazard escape to all roles and a retreat toward the living Tank for non-Tanks actively chased by the non-casting boss. Priority is death/inactive encounter, manual movement, escape, Tank retreat, normal positioning. Valid combat continues independently of these reactions. Escape accounts for agent clearance; normal position candidates and paths must avoid pending hazards. `NoSafeExit` exposes an unavailable escape without taking manual ownership.
- The boss now moves and attacks at its separate three-second interval, initially choosing the closest party unit and acquiring Tank after positive Tank damage. Its targeting, cast locks and navigation are owned by the boss feature.
- Boss death leaves party AI idle. Existing manual movement and debug health/log reset remain available. Reset revives units and re-arms decisions; it does not add an encounter-result screen.
- Disabling the boss combat object also pauses party actions and autonomous positioning; reactivation reevaluates current role/reaction conditions.
- The F8 debug panel shows every party state/target and boss state. **Keep Boss Alive (minimum 1 HP)** defaults off. Protection clamps the boss's resulting health only: valid attacks continue reporting their full hit damage and showing ranged tracers at one health, without a death event. For example, a 20-damage hit at five health reports 20 and leaves one; further hits still report 20. Debug kill also respects protection and explains why the boss remains alive. Disabling protection restores ordinary effective-damage/death behavior and does not revive a dead boss.
- Every party member has an attack-speed stat with a default of 1 attack per second; authored balance values are preserved. Tank, Melee DPS and Ranged DPS use `1 / AttackSpeed` for automated attacks. Healer retains separate basic healing at 1.5 seconds and has no new offensive action. T8 changes the boss's separate interval to three seconds. Debug attack/heal buttons remain immediate and independent of automated cooldowns.

## Implementation

- `PartyRoleBrain` owns role/patient decisions using assigned encounter references rather than global target searches. Decisions run every 0.2 seconds and position searches at most every 0.8 seconds per unit. Successful offensive actions use per-frame stat-derived scheduling independently of decision frequency; failed action/sight attempts retry with the decision cadence. Each success schedules from current game time, avoiding banked bursts. Basic healing still uses the existing serialized `actionInterval` (1.5 seconds), whose Inspector tooltip now identifies its healing-only purpose. Preferred spacing, safety and decision/search intervals remain tunable.
- `CombatStatsDefinition.AttackSpeed` and `CombatUnit.AttackSpeed` share value validation: nonpositive/nonfinite input falls back to 1; small positive values clamp to 0.01 to keep reciprocal intervals finite. A statless unit has a serialized fallback of 1. At most one automated action is issued per frame; very high requested rates remain limited by player frame rate.
- `CombatPositionSearch` checks 48 positions across three target-centered rings. Candidates must sample onto the matching NavMesh, be within action range, have clear sight and admit a complete path. Path length, preferred spacing and boss safety rank candidates. Failed searches report `NoReachablePosition` and retry without taking manual movement ownership.
- `UnitMovement.TryMove(..., Autonomous)` remains the path executor. Brains only cancel autonomous paths. Existing unit passability, selection colliders and navigation exclusions remain in effect.
- Actions use `CombatSystem` and its shared sight query. Self-healing uses normal range, sight, health and resolved-event checks. `SessionReset` re-arms decisions and boss timing. The combat resolver owns the optional boss damage floor so all damage producers agree.
- `PartyAiSceneSetup` updates current-arena references and calls the boss encounter setup, preserving component identity and tuning across repeats without regenerating geometry or overwriting health-bar settings. Existing navigation bakes are preserved; the separate boss bake is created if missing. Full combat/arena setup calls the same role wiring. Setup materializes/validates attack speed on the four existing party stats assets and saves only those assets. Valid custom speeds survive reapplication; unrelated Tank ranges and health-bar tuning are preserved.
- `CombatDebugPanel` caches brain references and only presents state. Controls, state rows and event history share a scroll view.
- T7's focused real Play-mode suite passed on 2026-10-20 in Unity 6000.6.6f1: advancing frames; unattended boss kill with all roles; boss timing/range/death; Tank-first, ally and self-healing; no revival; manual combat/resume; selection independence; melee/ranged/healing sight recovery around the saved west column; ranged retreat; unreachable-target failure; damage-floor outcomes; and reset recovery.
- The existing movement suite passed. Combined combat/health/ranged/sight checks passed with their original ten-pixel health-bar fixture applied only during Play mode; preserved requester edits set saved bars to five pixels and fail the legacy eight-to-twelve-pixel readability assertion. T7 does not change that assertion or those saved edits. Actual state/debug UI was inspected at camera sizes 8, 20 and 30. Repeated setup retained four party brains and one boss producer, and verification left the Editor in saved Edit mode.
- T7.1's extended combined AI Play-mode suite passed on 2026-10-20: protected threshold/repeated hits and positive damage logs, normal lethal recovery, tracer activation, immediate debug actions, invalid/statless speed values, all three attackers at 0.5/1/2/7 attacks per second, separate 1.5-second healing and two-second stationary boss timing, plus the existing unattended encounter/manual/sight/death/reset checks. Timing checks use player-frame tolerance and runtime stats copies; original stats assets remain at 1.

## Source files

- `raid-commander-game/Assets/_Game/Scripts/Units/PartyRoleBrain.cs`
- `raid-commander-game/Assets/_Game/Scripts/Units/BossBasicAttack.cs`
- `raid-commander-game/Assets/_Game/Scripts/Units/CombatPositionSearch.cs`
- `raid-commander-game/Assets/_Game/Scripts/Movement/UnitMovement.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatSystem.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatStatsDefinition.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatUnit.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatDebugActions.cs`
- `raid-commander-game/Assets/_Game/Scripts/UI/CombatDebugPanel.cs`
- `raid-commander-game/Assets/_Game/Editor/PartyAiSceneSetup.cs`
- `raid-commander-game/Assets/_Game/Editor/CombatSceneSetup.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/PartyAiVerification.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/AttackSpeedVerification.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/CombatVerification.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/MovementVerification.cs`
- `raid-commander-game/Assets/_Game/Scenes/RaidArena.unity`

## Current scope

One fixed encounter with four party roles, a mobile boss, [Wall of Flames and reactive positioning](boss-abilities-and-hazards.md), baked navigation, data-driven party attack speed, separate healing/boss timing, obstacle sight recovery, manual positioning priority, and debug state/floor controls. Protection preserves hit damage reporting independently of actual health loss. The toggle lasts for the Play session and remains enabled across health/log reset until toggled off or Play mode ends.

Threat tables, advanced formations, manual spells/projectiles, victory/defeat screens, a new restart flow and dynamic navigation rebaking are excluded. T8's bounded boss-targeting and ability scope is documented separately. Bounded position sampling does not guarantee solutions for arbitrary layouts or prove that no firing position exists. Existing combat ranges/stats remain authoritative. Earlier verification entries below/above describe their original T7/T7.1 checkpoints; T8 supersedes stationary boss timing.

Run **Raid Commander > Configure Autonomous Party AI** for AI-only current-scene wiring. **Verify Party AI in Play Mode** runs acceptance checks from Edit mode, exits Play mode and writes `Logs/t7-ai-verification.txt`. Existing combat/movement runners suspend autonomy within their temporary Play sessions so isolated regression fixtures retain their original scope.

**Verify Protected Combat and Attack Speed in Play Mode** invokes the combined T7/T7.1 runner, including its focused attack-speed checks. It restores runtime stats copies and all temporary health/position state before exiting Play mode.

## Revisions

| Version | Date (YYYY-MM-DD) | Summary |
|---|---|---|
| v1.2 | 2026-10-21 | Added all-role pending-hazard escape, chase retreat toward Tank, safe candidate/path filtering and explicit reaction states while preserving manual priority and stat-derived combat; integrated T8's mobile boss. |
| v1.1 | 2026-10-20 | Kept attacks/logs/tracers active at protected one health; added party attack-speed stats and independent per-frame offensive scheduling, preserving healing/boss/debug timing; passed protected-combat and 0.5/1/2/7 APS integration checks. |
| v1.0 | 2026-10-20 | Added four role brains, bounded navigation/sight positioning, stationary boss attacks, manual priority with continuing combat, self-healing, state debugging and the boss damage floor; passed focused Play-mode integration checks. |
