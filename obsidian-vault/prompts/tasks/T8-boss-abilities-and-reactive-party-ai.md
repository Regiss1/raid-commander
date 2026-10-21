# T8 — Boss abilities, Wall of Flames, and reactive party AI

## Tracking and handoff
- GitHub issue: https://github.com/Regiss1/raid-commander/issues/11.
- Status: implemented, verified and requester-approved on 2026-10-21 with `T8 OK`. Approval authorizes the reviewed T8 commit, push and issue completion. GitHub issue #11 tracks the final publication/completion state. Requester clarification resolved on 2026-10-20; verification completed on 2026-10-21.
- Depends on: T7, https://github.com/Regiss1/raid-commander/issues/10.
- Target implementer: Codex Sol 6.1.
- This is a separate task, as selected by the requester. T7 is completed and its original acceptance/review baseline remains preserved.
- The initial handoff was planning only. The subsequent explicit `Implement T8` request and recorded clarification answers authorize implementation; commit/push/completion still require task-specific review approval.
- Canonical specification: obsidian-vault/prompts/tasks/T8-boss-abilities-and-reactive-party-ai.md.
- Approved checkpoint: ignored `Logs/t8-spacing-review-scope.json`, 2026-10-21T20:03:43.128Z, 49 files including both review corrections. All approved file hashes matched before finalization; the branch had no unrelated unpublished commits. Exclude the three requester-owned balance asset edits and two recovery artifacts listed below. Earlier awaiting-review/uncommitted statements in this record describe historical checkpoints before this approval.

## Objective
Extend the T7 encounter with a mobile boss, damage-triggered Tank targeting, modular abilities, Wall of Flames, readable telegraphs, and party reactions that yield to player movement orders.

## Requester decisions (2026-10-20)
- Selected A: first cast starts 90 seconds after encounter activation/reset; later starts are 90 seconds apart; full width 4 world units; internal obstacles do not block flames.
- Encounter activation occurs on entering Play mode; the existing debug reset restarts the first-delay timer.
- Confirmed all remaining recorded refinements and the acceptance matrix, including three-second boss swings, half-speed retreat, one-hit flames and manual movement priority. Implement and verify the full scope, then leave changes uncommitted for review.
- T7 and T7.1 are completed. The planning-only restrictions below describe the earlier planning checkpoint; this explicit implementation request authorizes game changes, not commit/push/completion.

## Refined execution plan
- Extend existing boss, combat and per-unit movement/role owners; use a per-boss ability runner and configurable definitions with instance-owned timers.
- Define an explicit playable arena boundary and one clipped rectangle footprint shared by rendering, containment, damage and escape searches.
- Integrate mobile targeting/melee, Wall of Flames and party reactions into the current saved arena and repeatable setup without replacing customized geometry or balance assets.
- Verify the acceptance matrix through focused real Play-mode fixtures, one real 90-second production timing check, actual Game-view inspection and relevant existing regressions. Document outcomes and leave task changes uncommitted for requester review.

## Specification review: explicit refinements
1. Split unlimited targeting range from effect length: a target can be anywhere in the arena, while the effect ends at the arena boundary.
2. Use configurable ability definition assets plus small behavior scripts. Do not require a new copied script for every numeric variation. Runtime timers and targets belong to the boss instance, never shared assets.
3. Replace ambiguous Interval/HealthPercent with TriggerMode (Interval or HealthThreshold), and separate cadence, cooldown, initial delay, and threshold percentage. Health thresholds fire once per encounter; healing above the threshold does not rearm them.
4. Use existing roles Tank, Healer, MeleeDps and RangedDps; the requested DPS maps to MeleeDps. Choose uniformly among eligible living units, not roles. A single eligible role can still contain multiple units.
5. Add a small configurable range tolerance, entirely inside valid melee range, to prevent forward/backward oscillation. Use the existing XZ distance convention.
6. Backward movement means 50% of forward speed while facing the combat target, subject to valid navigation.
7. Boss swings remain range/sight checked. Moving does not suppress them; being out of range does. No catch-up burst of missed swings.
8. Define one authoritative hazard geometry for ground visuals, AI containment, and damage. Use party ground centers for hit membership and agent radius plus a small margin for escape clearance.
9. The red indicator is a brief impact visualization (default 0.35 seconds), not persistent damage. Damage occurs once at cast completion.
10. Player movement overrides positioning only, preserving T7: valid attacks/heals continue during manual movement, escape, and retreat.
11. Explicit movement priority: death/inactive encounter, manual order, telegraph escape, retreat toward Tank, normal role positioning.
12. A Tank damage event during a cast updates the underlying combat target but cannot rotate or move the locked cast. Restore the previous combat target unless Tank acquisition or target invalidation superseded it.
13. No new victory/defeat UI, threat table, mod runtime, general editor tool, or unrelated role overhaul.

The requester confirmed these implementation refinements and the full acceptance matrix during T8 clarification. Timing/geometry and encounter activation are resolved above.

## Existing integration baseline
Reinspect the completed T7 implementation before editing. At planning time T7 is in progress and source already contains PartyRoleBrain, CombatPositionSearch, BossBasicAttack and PartyAiSceneSetup.
- Extend the existing role brain and command producers; retain UnitMovement as the executor.
- Replace/extend stationary BossBasicAttack rather than leaving a second attack loop running.
- Reuse CombatSystem, CombatUnit, CombatLogService and the existing session reset event.
- CombatEventType already defines SpellCastStart, SpellCastSuccess, SpellCastFailed and SpellDamage. Activate these through the central resolver/event pipeline; do not create a parallel log.
- CombatSystem.ApplyDamage currently lacks spell ID propagation. Extend resolution compatibly so ability hits retain their spell identity and ordinary damage/death behavior.
- Preserve mutual unit passability, obstacle navigation, existing selection, health bars, ranged tracers, healer priority and manual override lifecycle.

## Work package 1: boss targeting, movement and melee
- On encounter activation, acquire the closest living party member by XZ distance. Before Tank acquisition, reevaluate closest living target on throttled AI decisions; stable encounter ID breaks ties.
- A successful positive damage event from a living Tank against this boss establishes Tank target lock. Other roles cannot steal it. No threat accumulation or decay.
- If the locked Tank dies or becomes invalid, clear the lock and acquire the closest living party member. No eligible party means idle.
- Follow the combat target only far enough to enter the melee range band. Back away when too close; approach when too far. Stop within the band.
- Keep facing the combat target during retreat. Forward speed is configurable; retreat speed is exactly half. Restore normal speed after retreat/cast/reset.
- Use NavMesh and the existing executor with complete-path validation. Never teleport, pass through arena obstacles, or force an unreachable spacing destination. If spacing cannot be maintained, hold a valid location and retry with throttling.
- Basic attacks have a 3-second interval, use existing basic damage, and may resolve while moving when alive, in range and with the existing AI sight requirement.
- On initial activation, first swing is due after 3 seconds. Out-of-range time does not accumulate multiple swings. Cast start suppresses swings; cast completion schedules the next swing 3 seconds later.
- Expose combat target, chasing state and casting state for party reactions. Chase means active approach navigation toward the current combat target, not retreat or being stationary.

## Work package 2: modular ability model and runner
Provide an Inspector-editable boss loadout of ability definitions. Adding/removing a definition must not require changes to boss controller code.

Definition fields:
- Stable ability ID and display name.
- TriggerMode: Interval or HealthThreshold.
- InitialDelaySeconds, IntervalSeconds, CooldownSeconds, HealthThresholdPercent.
- Damage, CastTimeSeconds (0 is supported).
- AllowedTargetRoles and random eligible-unit selection.
- TargetRangeMode: Unlimited or Limited; TargetRange for Limited.
- DeliveryMode: GroundArea or DirectTarget.
- Shape and shape parameters: rectangle width/length initially; unsupported shapes must fail validation explicitly.
- LengthMode: Fixed or ToArenaBoundary.
- LockFacingOnCastStart, StopMovementDuringCast, SuppressAutoAttacksDuringCast.
- Telegraph and impact presentation settings/duration.

Keep shape extensibility small: implement the rectangle needed here, not a library of speculative shapes. Implement/test minimal DirectTarget and HealthThreshold paths with fixtures; do not add extra player-facing boss abilities.

Runner:
- At most one active cast per boss; resolve simultaneous readiness in stable loadout order.
- Interval readiness and completion cooldown are separate gates. Wall of Flames uses the confirmed start-to-start cadence with zero additional completion cooldown.
- A threshold becomes pending when health first reaches or crosses the configured percentage; consume it once on cast start. Reset rearms it. If temporarily unable to start, retain one pending activation.
- No eligible target means skip starting and retry on a throttled decision; do not create phantom cast logs or build an unbounded queue.
- Zero-time casts complete once in the same update with ordered start/success/damage events.
- Disable, boss death and encounter reset cancel casts and clean up visuals/timers. A cancelled cast causes no damage.
- Losing the aimed-at unit after ground-cast start does not cancel its fixed ground effect. A direct-target cast whose target becomes invalid fails without damage.
- Validate invalid percentages, negative durations, zero widths, empty role filters, missing boundaries and unsupported shapes. Report a useful setup error without repeated per-frame logging.

## Work package 3: Wall of Flames
- Name: Wall of Flames. Damage: 95. Cast time: 3 seconds.
- Target filter includes all four party roles; select one living party member uniformly, without targeting distance limit.
- Select target, stop movement, face it, then snapshot cast origin and heading. If target coincides with boss position, use the current valid planar forward direction.
- Do not chase the ability target. Keep the separate normal combat target; never let combat facing or NavMesh rotation override cast lock.
- Spawn a yellow rectangular ground telegraph extending forward from the boss's front footprint to the arena wall in that heading.
- Use explicit arena boundary geometry, not arbitrary wall names, camera bounds, target distance, or infinity. Clip the footprint at the playable arena boundary, including oblique casts near corners.
- Confirmed width is 4 world units; internal obstacles do not block flames; cast starts are 90 seconds apart.
- Boss position and orientation remain fixed for all 3 seconds, including when the selected unit moves, dies, or a Tank hits the boss.
- At completion, take one membership snapshot of living party members within/on the footprint. Resolve 95 damage once per member through CombatSystem; multiple colliders must never cause duplicate hits.
- Turn the footprint red for the brief impact display. The hazard is no longer pending after damage resolves; late entrants take no damage.
- Restore the valid pre-cast combat target, subject to newer Tank acquisition; otherwise reacquire according to boss rules.
- If no unit is hit, still log a successful cast.
- Publish SpellCastStart and SpellCastSuccess with boss ID and ability ID; publish SpellDamage for each actual hit with victim ID and effective damage. Keep existing lethal damage then UnitDied ordering. Effective damage can be below 95 when remaining health is lower.
- Cancellation can publish SpellCastFailed before cleanup; session reset then clears history using the existing reset contract.

## Work package 4: party reactions
- Apply reactions to all roles, including Tank for telegraph avoidance.
- If inside a pending ground hazard and no manual order owns movement, find the nearest reachable exit beyond its edge. In open space use the nearest geometric edge; if blocked, evaluate other exits and choose the shortest valid path.
- Account for agent clearance, floor elevation, arena limits, obstacles and other active hazards. Never choose an endpoint still inside another hazard when a safe reachable option exists.
- Cancel only this autonomous escape when safely outside or when the pending hazard ends. Reevaluate normal behavior on the next decision; do not cancel a player order.
- If no safe exit is reachable, expose a useful AI/debug state and retry at bounded frequency. Do not teleport or promise avoidance of unavoidable hits.
- Prevent normal role positioning from immediately walking back into the same pending telegraph.
- Otherwise, if the boss is not casting and is targeting AND actively chasing this non-Tank party member, move toward the living Tank's reachable current position, refreshing as needed. Stop the reaction when the predicate ends; if Tank is unavailable, use normal role behavior.
- Escaping takes priority over retreating to Tank. Continue valid attacks/heals with existing range, sight and action timers.
- An accepted manual order immediately overrides both reactions, even if it enters danger. When that order completes, fails or is cancelled, reevaluate current conditions rather than resuming a stale path.
- Selection changes have no effect on other units.

## Work package 5: scene, reset, debugging and verification
- Wire the boss NavMeshAgent/executor, controller, loadout, Wall of Flames definition, arena boundary and telegraph renderer into the existing saved arena through supported Unity tooling.
- Extend idempotent setup utilities; preserve customized arena geometry, asset GUIDs and T7 changes. Do not regenerate the arena blindly.
- Show boss combat target/cast state and party reaction state using existing debug facilities.
- Preserve T7's optional boss one-health toggle. Use it or isolated fixtures to observe a 90-second ability in tests without retuning production balance.
- Reset clears cast state, targets/lock, pending hazards, temporary speed changes and timers, and re-arms health thresholds. Death/disable removes stale movement and subscriptions.
- Update relevant system notes and feature documentation with implemented behavior only after verification.

## Acceptance and verification matrix
1. Closest-unit acquisition, stable ties, Tank damage acquisition, no non-Tank stealing, Tank death fallback, no-target idle.
2. Approach stops in melee range; closing target causes half-speed retreat without flipping facing; moving-away target is pursued; obstacle/boundary cases never escape navigation.
3. Swings occur at 3-second cadence while moving in valid range, never out of range, never during casting and never in a catch-up burst.
4. Add/remove an ability in loadout; multiple boss instances do not share runtime cooldowns. Test interval scheduling, threshold once-per-reset, zero-time cast and simultaneous readiness.
5. Wall begins at the chosen first delay, casts for exactly 3 seconds and recurs at the agreed cadence. Use test clock/configuration overrides for fast cases plus one real production-timing check.
6. All roles can be randomly aimed at. Cast locks orientation/origin, reaches the correct boundary, never chases aim target, and restores combat targeting correctly.
7. Verify yellow footprint and red impact from actual Game view at relevant zoom levels. Damage and AI queries match the displayed footprint at side, start, end and corner boundaries.
8. At resolution, inside/on-edge members take one 95-point hit (subject to remaining health); outside members take none; no lingering or collider-duplicated damage.
9. Verify start, success, hits and death log identity/order, no-hit success, and cancelled cast without damage. Disabling presentation/logging does not disable resolution.
10. Escape uses nearest reachable exit, stops safely, handles obstacles/overlap and does not reenter the warning through normal positioning.
11. Chase reaction requires all predicates and a living Tank. Manual commands override reactions while valid attacks/heals continue; arrival/failure/cancellation restore current AI decisions.
12. Boss/party death, target death during cast, disable and session reset leave no stale telegraphs, movement, timers or event handlers.
13. Existing T7 role behavior, selection, manual orders, obstacle sight, health bars, debug damage floor and combat integration remain intact.
14. Unity compilation and relevant Play-mode suites pass; repeated setup does not duplicate components/assets or remove arena customization. Record any verification gaps.

## Exclusions and completion boundary
No threat table, taunt system, persistent flames, damage over time, new party spells, manual spellcasting, formations, mod loading, generalized ability authoring UI, new encounter results/restart screens, or extra production boss abilities.
After implementation and verification, leave task changes uncommitted and mark the issue awaiting review. Task-specific approval is required for commit, push and completion.

## Implementation prompt for Codex Sol 6.1
Implement T8 from this canonical specification using the implementing-game-development-tasks skill. First confirm T7 is completed, resolve the pending requester choice, inspect current repository/task/issue state, and preserve unrelated work. Extend existing ownership rather than duplicating T7 systems. Complete gameplay, saved scene wiring, repeatable setup, focused runtime/visual verification and vault documentation. Stop at the uncommitted awaiting-review checkpoint with results and any verification gaps.

## Required correction: Play-entry boundary validation (2026-10-21)

- Requester explicitly requested applying the proposed fix. This is an unmet original T8 acceptance criterion, retained under T8 / issue #11; no new scope or task number. The correction is implemented and verified, included in the uncommitted T8 review scope.
- Reproduced on fresh Play entry: `SetupError` reports invalid ground geometry and cached readiness is false even though the same runtime boundary is active, its collider is enabled with XZ size 33 by 33, and current definition validation succeeds. Early `OnEnable` validates before the boundary peer's activation/physics initialization finishes; loadout identity caching then retains the failed result. A regression run also confirmed that geometry calculation alone did not remove the premature warning, so initial validation now waits for `Start` while retaining the `OnEnable` time anchor.
- Correct boundary geometry to use the authored box and transform independently of physics initialization. Revalidate setup at the existing bounded decision cadence, report specific invalid dependencies/dimensions without repeated warnings, and recover without restarting encounter/interval/cooldown/threshold state.
- Verify fresh Play entry before any fixture reset, invalid setup and repair, warning deduplication, reset scheduling, repeated activation, and a runtime-only 90-second timing fixture. Preserve current requester-authored 15/15-second delay/interval and all other assets/scene tuning; do not replace production data to run tests.
- Original review checkpoint: ignored `Logs/t8-final-review-scope.json` (2026-10-21T17:40:08.967Z, 49 files); pre-correction snapshot: `Logs/t8-warning-planning-checkpoint.json`. Keep original evidence historical and capture corrected results separately. Changes remain uncommitted for a fresh T8 review checkpoint.

### Correction verification and review checkpoint

- **PASS:** fresh Play entry before fixture reset, using the existing Editor loadout: first cast at 15.08256 seconds, successful resolution, and 5,606 advancing frames across the focused run. No premature boundary warning. Added **Verify Boss Ability Startup and Recovery in Play Mode** to repeat this check.
- **PASS:** disabled/degenerate collider, missing boundary/hazard references, zero/NaN width and infinite length are rejected with specific diagnostics. Unchanged errors do not repeat every decision. Repair clears errors without replacing the loadout or restarting initial-delay, interval or completion-cooldown clocks; consumed health thresholds remain consumed. Debug reset and reactivation rearm the initial delay.
- **PASS:** rerun of the full T8 fast suite after correction, including geometry, targeting/movement, loadout gates/isolation, spell/damage lifecycle and party reactions. An intermediate warning-history assertion was corrected to clear deliberate fault-fixture warnings before a new test session; it was not a new gameplay failure.
- **PASS:** real-time 90-second baseline through a runtime-only definition clone: first cast after 90.09099 seconds, resolution after 3.004181 seconds, 20,653 advancing frames. The existing warning/impact captures were refreshed by that suite. Original historical verification above/below remains separate evidence, not a claim that every T7 suite was rerun for this correction.
- **PASS:** compilation succeeds; final current Console has zero errors and zero warnings. Retained diagnostics, including intentional invalid-setup warnings and intermediate test failures, are archived in ignored `Logs/t8-correction-console.json`; the Console was not manually cleared during correction verification.
- Final Editor state: saved scene, Edit mode, no active cast or missing scripts, all four role brains enabled, time scale 1, background execution false, four five-pixel health bars. The requester's dirty Inspector ability values remain 15/15 seconds; the saved asset remains byte-identical at the original 90/90 baseline. No scene or asset was regenerated or saved over those edits.
- Correction changes only six files within the original 49-file review scope: `ArenaBoundary`, `BossAbilityRunner`, `BossAbilityDefinition`, `BossEncounterVerification`, this task and the dedicated boss system note v1.1. Other original T8 files, requester stat edits and recovery artifacts are preserved. New hashes/results are recorded separately in ignored `Logs/t8-correction-review-scope.json`; original review hashes are retained. All T8 changes remain uncommitted pending task-specific approval.

## Required correction: stable mutual melee spacing (2026-10-21)

- The requester asked to implement the recommended target-aware fix after clarification. This corrects the open T8 anti-oscillation/spacing acceptance criterion under issue #11, without a separate task number.
- Existing boss range 3.5 produced a holding band of 3.05–3.4, while Tank must close within its range of 2. The prior isolated movement fixture kept Tank AI disabled and missed the autonomous chase/retreat feedback loop.
- Retreat entry is half the smaller of boss and combat-target melee ranges (currently 1 world unit for Tank). Once retreat starts, continue only until entry plus the configured range tolerance (currently 1.35), clamped to boss melee range. Otherwise hold position whenever the target is visible and inside boss melee range, including the outer range edge.
- Approach/retreat navigation remains complete-path, obstacle-aware and within melee reach; retreat remains half speed and facing the target. Target changes, casting, death/disable and reset must not retain stale retreat state. Combat cadence and player movement priority remain unchanged.
- Verify natural autonomous Tank/boss approach and stable mutual combat, stationary target at mutual range, deliberate too-close movement, retreat hysteresis/release, pursuit after the target leaves range, target changes and existing T8 regressions. Preserve saved scene/asset tuning and prior boundary/startup correction. Changes remain uncommitted for a new T8 review checkpoint; pre-edit scope is retained in ignored `Logs/t8-spacing-before-scope.json`.

### Spacing correction verification and review checkpoint

- **PASS:** **Verify Stable Boss Melee Spacing in Play Mode**, at normal game speed, lets both autonomous units approach naturally, then checks nine seconds of stationary boss positioning, stable mutual attack reach and positive melee attacks from both units. The successful run held at 1.28904 world units and recorded 2,127 advancing frames during that nine-second observation. This catches the original chase/retreat feedback loop rather than freezing the Tank.
- **PASS:** stationary Tank at its own two-unit attack range and just inside boss outer melee range leaves the boss stationary. A deliberately close Tank starts half-speed retreat while the boss faces it; retreat releases inside mutual reach, stops moving and does not restart within the hysteresis gap. A target leaving melee range restores full-speed pursuit. Target death/replacement clears the former target's retreat state.
- **PASS:** rerun of all T8 fast fixtures, including the new natural-combat regression at accelerated test speed and existing targeting/cast/damage/party reaction/cleanup checks. The older retreat-destination assertion now checks the approved release distance rather than the obsolete outer holding band. An initial exact-edge fixture was moved 0.01 units inside legal reach so NavMesh sampling cannot invalidate the intended in-range precondition.
- **PASS:** existing combined role/encounter/override/sight/death/reset/debug-floor and attack-speed verification. Compilation succeeds; final current Console has zero errors and zero warnings. Diagnostics are retained in ignored `Logs/t8-spacing-console.json` without manually clearing history.
- No scene, asset, navigation bake or setup utility needed alteration: the existing controller/serialized tolerance applies the corrected policy. Final Editor state is saved Edit mode, all brains/boss enabled, time scale 1 and background execution false. Requester balance edits, five-pixel bars, recovery artifacts and dirty Inspector 15/15-second ability values remain preserved.
- This correction changes four files relative to the previous T8 review checkpoint: `BossBasicAttack`, `BossEncounterVerification`, this task and the boss system note v1.2. The full T8 review still contains 49 files, all uncommitted. Current hashes and passing reports are recorded separately in ignored `Logs/t8-spacing-review-scope.json`; earlier review checkpoints remain intact. Issue #11 remains open awaiting review; no commit, push or completion is authorized by the implementation request.

## Implemented result

- Extended the existing boss producer with closest-unit acquisition, damage-triggered Tank locking and death fallback, complete-path NavMesh spacing, half-speed retreat/facing and independent three-second swings. A cast retains the valid pre-cast combat target unless Tank acquisition or invalidation supersedes it; normal closest-target decisions resume afterward. The reviewed spacing correction holds throughout valid visible melee reach, with target-aware close retreat and a separate release distance.
- Added asset-defined loadouts and per-boss runtime readiness/cast ownership. Interval and completion-cooldown gates, pending once-per-reset health thresholds, zero-time casts, direct-target failure and loadout-order arbitration are implemented without shared mutable asset state.
- Added the agreed Wall of Flames asset, explicit playable BoxCollider boundary, one clipped footprint shared by visuals/damage/AI, yellow warnings and brief red impact, one-time deduplicated victim snapshots and normal spell lifecycle/damage/death events through `CombatSystem`.
- Integrated all-role escape and non-Tank chase retreat into the existing role brain and movement executor, preserving manual priority, valid combat during positioning, healing policy, attack-speed stats, selection and reset behavior.
- Updated current-scene setup and generation paths. A separate boss-size profile and saved NavMesh provide 0.95-unit static clearance without changing the party mesh. Scaled agent offsets keep the larger boss grounded. Repeated encounter setup preserves existing navigation assets, geometry, loadout and tuning; the movement-bake command refreshes both meshes after geometry edits.
- Dedicated system documentation: [Boss abilities and ground hazards v1.2](../../systems/boss-abilities-and-hazards.md). Updated AI/combat/movement notes, project structure, READMEs and checklist.

## Verification record (2026-10-21)

- **PASS:** final fast real Play-mode suite, including clipped edges/corners and invalid setup; closest targets/ties/Tank acquisition/fallback; approach, retreat/facing, body/floor clearance and three-second swings; cast locks and target restoration; threshold/direct/zero-time/interval/cooldown gates; random eligible roles/units, loadout add/remove/order and independent clocks for two bosses.
- **PASS:** Wall hit snapshots, exact-on-edge and effective lethal damage, spell identity/order, lost ground aim, multiple colliders without duplicate hits, zero-hit success, no lingering damage, resolution without presentation/logging, disable/death/reset cleanup, all-role escape/clearance, blocked/overlapping/unavoidable exits, manual movement with continuing combat, no normal reentry and chase retreat toward a living Tank.
- **PASS:** existing combined role and attack-speed suite, including unattended boss defeat, Tank-first/ally/self healing, sight recovery, manual combat/resume, independent selection, protected-hit continuity and 0.5/1/2/7 APS. Its coordinate fixtures isolate the new boss agent when teleporting the old placeholder outside navigation; actual boss movement is covered by T8 fixtures.
- **PASS:** existing combat/reactive-health/ranged/line-of-sight suite at normal game speed. Its original ten-pixel bars are applied only inside temporary Play mode; the saved five-pixel customization still falls below the legacy readability assertion and is preserved. The accelerated wrapper initially failed a movement-arrival assertion; restoring the suite's normal time passed all checks.
- **PASS:** existing movement suite with synthetic Input System events, terminal command states, manual ownership, static obstacle paths, concurrent orders, pass-through/shared destinations and selection colliders. Its expected executor count now includes four party units plus T8's independent boss.
- The interrupted run's stale production-configuration failure is not reproduced: the resumed Editor asset matches the agreed 90/90/0-second delay/interval/cooldown, three-second cast, four-unit width and 95 damage. Compilation currently succeeds. Verification evidence remains under ignored `raid-commander-game/Logs/`.
- **PASS:** final real production timing: first Wall started after 90.05474 seconds and resolved after 3.003838 seconds, with 21,371 advancing player frames recorded. Production checks continuously verify boss body clearance and ground alignment. The later standalone frame-wait reached Edit mode after verification had already exited; its unchanged frame count is expected outside Play and is not the timing evidence.
- **PASS:** actual yellow warning and red impact Game views at orthographic sizes 8, 20 and 30, including clipped casts at the perimeter and the existing debug/history/HUD. Captures: ignored `Logs/t8-yellow-*.png` and `Logs/t8-red-*.png`.
- **PASS:** repeated current-scene setup retained 11 feature/navigation components, eight asset byte hashes/GUIDs, all collider transforms, a saved scene and exactly two agent profiles. The original party NavMesh is unchanged. Inspector `Everything` uses -1 for all roles; validation now supports that standard value and rejects other unsupported masks. The existing Everything selection was retained, and repeated setup is stable after serialization.
- Final lifecycle coverage also checks party idle/action suspension while the boss object is disabled, and reevaluation after reactivation. A transient Pipeline connection drop during script reload did not execute its verification request; reconnecting and restarting that request uses the same saved task scope.
- **PASS:** final compilation and Console ground-truth check: not compiling, compilation failure false, zero current errors/warnings, no missing scene scripts. The Editor is out of Play mode with a saved scene, all four brains and the boss runner enabled, no active cast, `timeScale = 1`, `runInBackground = false` and all four saved bars at five pixels. Resolved diagnostic history is archived in ignored `Logs/t8-console-history.json` before clearing the Console.
- Final file hashes and exclusions are recorded in ignored `Logs/t8-final-review-scope.json`. Source/Markdown whitespace checks pass; Unity-emitted blank `m_Name` fields retain normal serializer trailing spaces. Task filename/number validation passes for all eleven tasks.

## Review scope and preserved requester work

- Runtime: new `Abilities/` definitions, runner, footprint, hazard collection and presentation; new `Encounters/ArenaBoundary`; extensions to the boss/party producers, position search, scaled movement feet, combat spell identity/facing lock and debug state/history.
- Integration: boss encounter/navigation setup, existing AI/movement setup integration, saved `RaidArena.unity`, Wall of Flames data and telegraph material, boss NavMesh asset and one additional profile in `ProjectSettings/NavMeshAreas.asset`, with associated metadata. The original party NavMesh asset is unchanged.
- Verification: new `BossEncounterVerification`; compatible fixture/isolation updates to party/attack-speed, combat, movement and ranged runners. Documentation: this task, the dedicated boss note and relevant system/project notes.
- Exclude requester-owned stat edits in `BossCombatStats.asset` (damage 45 to 30), `DpsCombatStats.asset` (damage 40 to 90) and `HealerCombatStats.asset` (healing 25 to 20). Preserve all committed balance tuning, four five-pixel bars, Tank ranges and customized geometry.
- Preserve the new untracked `Assets/_Recovery/0 (1).unity` and its metadata; these are recovery artifacts outside T8's review scope. Logs/captures and temporary verification helpers are ignored and excluded from publication.
- All T8 changes remain uncommitted. Implementation/verification does not authorize commit, push, merge or issue completion; a later task-specific approval is required.

