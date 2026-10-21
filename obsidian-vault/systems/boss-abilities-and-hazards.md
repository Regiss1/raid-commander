# Boss abilities and ground hazards v1.2

**Status:** implemented; T8 requester-approved on 2026-10-21, including startup validation and mutual-spacing corrections. See the linked task issue for publication/completion state.

## Overview

The arena's boss produces movement and melee requests independently of party selection. A per-boss runner executes Inspector-configured abilities, while an encounter-local hazard collection shares pending ground footprints with party reactions. Combat resolution and logging remain in the existing combat feature.

## Behavior

- On Play activation/reset the boss acquires the closest living party member by XZ distance; encounter ID breaks ties. Positive damage from the living Tank locks targeting to Tank. Other roles cannot steal it. Tank death releases the lock and no living party leaves the boss idle.
- The boss holds position whenever its visible target is within melee range, allowing a shorter-reach Tank to approach and attack. It retreats only below half the smaller of its own and the target's melee ranges (currently 1 unit for Tank). Once retreating, it stops at that distance plus the configured tolerance (currently 1.35 units), capped by boss melee range. This separates retreat entry from release rather than forcing both units into the boss's former narrow outer holding band. Retreat remains half forward speed while facing the target. Unavailable spacing holds a reachable position and retries. Basic swings require range/sight and occur every three seconds, including while moving. The first swing and the first swing after a cast wait three seconds; missed swings do not accumulate.
- Wall of Flames first starts 90 seconds after Play activation/reset and subsequent starts are 90 seconds apart. It aims uniformly at an eligible living unit across all four roles, with unlimited targeting distance. Its four-unit-wide yellow footprint extends from the boss's front to the explicit playable boundary, clipped at oblique corners. Internal obstacles do not stop flames.
- The boss stops and locks facing/origin for three seconds. At completion the footprint turns red for 0.35 seconds, and living party ground centers inside/on it take one 95-point hit, capped by remaining health. Late entrants take no damage. Moving or losing the aimed-at unit does not move/cancel a ground cast. Combat targeting remains independent of cast aim: retain a valid pre-cast combat target through completion unless Tank acquisition or target invalidation supersedes it, then resume normal closest-target decisions.
- Movement priority is death/inactive boss, manual order, pending-hazard escape, non-Tank retreat toward a living Tank when actively chased, then normal role positioning. Escape seeks the shortest reachable safe exit with agent clearance and avoids unsafe endpoints in overlapping hazards. No reachable exit reports `NoSafeExit` and retries. Normal autonomous paths cannot reenter/cross a pending footprint. Manual orders may enter danger and valid attacks/heals continue during every positioning reaction.
- The existing debug panel shows combat target, boss movement/casting, countdown, party reaction states and spell IDs in event history. Boss protection, immediate debug actions, F8 and the health/log reset remain available. No results screen or new restart flow is added.

## Implementation

- `BossBasicAttack` is the single boss targeting/movement/swing producer, extended from T7 rather than duplicated. It submits complete-path commands to `UnitMovement`. Changing combat target cancels the previous target's navigation and retreat state. Retreat destinations lie beyond the release distance, with an arrival margin for agent braking; approach destinations remain inside attack reach. The existing scene tolerance now controls retreat hysteresis, so no new serialized component or setup/rebake is needed. `CombatUnit.FacingLocked` prevents combat-facing helpers from turning a locked caster; the boss agent has automatic rotation disabled.
- `BossAbilityDefinition` stores stable ID/name, interval or once-per-reset health threshold, initial delay/interval/completion cooldown, damage/cast time, role/range/delivery filters, rectangle geometry, cast flags and presentation settings. Unity's Inspector `Everything` role mask (-1) is accepted as all four supported roles; empty and other unsupported masks fail validation. `BossAbilityRunner` owns timers, target and cast state per instance; loadout order resolves simultaneous readiness. Threshold crossings remain pending until a target exists and healing never rearms a consumed threshold. Zero-time casts resolve ordered start/success/damage in one update. Direct-target loss fails without damage.
- The runner captures activation time in `OnEnable`, then first validates in `Start` after scene peers finish their initialization. Subsequent validation runs at the existing 0.1-second decision cadence while idle. Missing boundary/hazard references, disabled or invalid boundary geometry, and invalid ability dimensions have distinct diagnostics; each unchanged error is logged once per readiness entry. Repair clears the error and restores readiness without replacing its interval/cooldown/threshold state. Reset/reactivation creates fresh encounter state with the authored initial delay. Initial uninitialized/dead health cannot queue a threshold cast.
- `ArenaBoundary` references an explicit horizontal BoxCollider volume. Bounds derive from its authored center/size and transform rather than physics registration, and validity requires enabled/active components and positive finite XZ bounds. The saved volume spans the perimeter walls' inner faces, XZ -16.5 to 16.5. It is a trigger on Ignore Raycast and is excluded from navigation baking. It is editable independently of wall names or camera bounds; repeated setup preserves it.
- `HazardFootprint` clips a convex rectangle to that boundary. The same polygon drives renderer vertices, closed-edge hit tests, clearance, and path intersection. `GroundHazards` contains pending footprints only; impact visuals are presentation-only. Victims are snapshotted once and deduplicated by unit, independent of colliders.
- `CombatSystem.PublishCast` activates the existing cast event schema, and optional spell IDs propagate through spell damage and lethal death events. Ordinary and protected-boss damage semantics remain unchanged. Disable/death cancels without damage; reset cancels without adding a failure entry after the encounter log has been cleared.
- `BossEncounterSceneSetup` wires the current arena without regeneration. `BossNavigationSetup` creates a separate Raid Boss profile (0.95 world-unit bake clearance, height 3) and saved `RaidBossNavMesh.asset`; the four party agents and their original mesh remain unchanged. Both bakes collect Default/Ground physics geometry and ignore both unit layers. Boss automatic avoidance stays disabled and units remain mutually passable. The scaled boss's agent base offset and movement feet calculation account for transform scale.
- **Configure Boss Encounter and Wall of Flames** (also **Configure Autonomous Party AI**) reapplies scene wiring and preserves existing loadout, ability values, component identity, balance assets and authored boundary. **Configure Movement and Bake Navigation** rebakes both existing surfaces after geometry changes, preserving their asset GUIDs. The full arena-generation/combat paths also include the feature.

## Source files

- `raid-commander-game/Assets/_Game/Scripts/Abilities/BossAbilityDefinition.cs`
- `raid-commander-game/Assets/_Game/Scripts/Abilities/BossAbilityRunner.cs`
- `raid-commander-game/Assets/_Game/Scripts/Abilities/HazardFootprint.cs`
- `raid-commander-game/Assets/_Game/Scripts/Abilities/GroundHazards.cs`
- `raid-commander-game/Assets/_Game/Scripts/Abilities/GroundHazardVisual.cs`
- `raid-commander-game/Assets/_Game/Scripts/Encounters/ArenaBoundary.cs`
- `raid-commander-game/Assets/_Game/Scripts/Units/BossBasicAttack.cs`
- `raid-commander-game/Assets/_Game/Scripts/Units/PartyRoleBrain.cs`
- `raid-commander-game/Assets/_Game/Scripts/Units/CombatPositionSearch.cs`
- `raid-commander-game/Assets/_Game/Scripts/Movement/UnitMovement.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatSystem.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatUnit.cs`
- `raid-commander-game/Assets/_Game/Scripts/UI/CombatDebugPanel.cs`
- `raid-commander-game/Assets/_Game/Editor/BossEncounterSceneSetup.cs`
- `raid-commander-game/Assets/_Game/Editor/BossNavigationSetup.cs`
- `raid-commander-game/Assets/_Game/Editor/PartyAiSceneSetup.cs`
- `raid-commander-game/Assets/_Game/Editor/ArenaMovementSetup.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/BossEncounterVerification.cs`
- `raid-commander-game/Assets/_Game/Data/BaseGame/Abilities/WallOfFlames.asset`
- `raid-commander-game/Assets/_Game/Scenes/RaidArena.unity`
- `raid-commander-game/Assets/_Game/Scenes/RaidBossNavMesh.asset`
- `raid-commander-game/ProjectSettings/NavMeshAreas.asset`

## Current scope

One production boss ability, rectangular ground damage, minimal direct-target and health-threshold support, static baked arena navigation and four party reactions. The finite edge samples cannot guarantee an exit in every arbitrary obstacle arrangement; failures remain explicit and retry at bounded frequency. Boundary authoring currently uses a horizontal axis-aligned box. Casts use game time; party offensive rates remain stat-derived and healing retains its separate interval.

The fast real Play-mode suite passed on 2026-10-21 for geometry, boss acquisition/lock/fallback, approach/retreat/facing and swing cadence, cast/damage events, interval/cooldown/threshold/direct-target paths, random roles/units, loadout changes/order, multiple boss clocks, all-role escape, manual combat/priority, overlap, unavailable/obstructed exits and chase reactions. Ground-cast cases cover victim snapshot/deduplication, lost aim, zero-hit success, independent presentation/logging and cancellation/death/reset. Target restoration and lower spacing bounds have explicit regression checks. Existing role/attack-speed, combat/health/ranged/sight and movement/input suites also pass. The health suite uses its original ten-pixel bars only inside its temporary Play session; saved five-pixel bars and user balance assets are preserved. See T8's task record for timing, visual evidence and setup verification.

The T8 startup correction was verified separately on 2026-10-21: fresh Play entry successfully cast with the requester's current 15-second Inspector timing before any fixture reset. **Verify Boss Ability Startup and Recovery in Play Mode** covers invalid setup/repair, deduplicated diagnostics, preserved interval/cooldown/consumed-threshold state, debug reset and reactivation. The full fast suite passed again, and a runtime-only 90-second definition clone started after 90.09099 seconds and resolved after a 3.004181-second cast. The saved ability asset and the dirty Inspector timing remain untouched. Final compilation succeeds with zero current Console errors/warnings; all correction changes remain uncommitted for T8 review.

The mutual-spacing correction was verified on 2026-10-21 through **Verify Stable Boss Melee Spacing in Play Mode**: both units approach autonomously and sustain nine seconds of stable mutual combat while the boss holds its position. Normal-speed and accelerated fixtures cover close retreat/release, holding within the hysteresis gap, pursuit outside attack reach and target replacement. The complete T8 fast suite and existing combined role/attack-speed suite pass. Scene/asset/navigation configuration is unchanged; the final current Console has zero errors/warnings. T8 remains uncommitted and awaiting review.

These verification paragraphs record the pre-approval review checkpoints. The requester subsequently approved the complete T8 scope with `T8 OK` on 2026-10-21, authorizing its scoped commit/push and completion of [issue #11](https://github.com/Regiss1/raid-commander/issues/11). Requester-owned balance changes, recovery artifacts and dirty Inspector test timing remain outside publication scope.

## Revisions

| Version | Date (YYYY-MM-DD) | Summary |
|---|---|---|
| v1.2 | 2026-10-21 | Replaced the narrow outer melee band with target-aware close retreat and distinct release distance, allowing stable mutual Tank/boss combat; clear retreat state when targets change. |
| v1.1 | 2026-10-21 | Corrected early Play-entry validation and permanently cached invalid readiness; derive boundary geometry independently of physics startup, recover setup without resetting clocks, and report precise deduplicated errors. |
| v1.0 | 2026-10-21 | Added mobile boss targeting and spacing, per-instance configurable abilities, clipped Wall of Flames, shared pending hazards, party reactions and boss-size navigation; verified under T8. |
