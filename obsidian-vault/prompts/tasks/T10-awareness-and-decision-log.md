# T10 — Awareness and decision log

## Tracking and handoff
- Status: implemented; requester approved T10 for commit, push and completion on 2026-10-22.
- GitHub issue: https://github.com/Regiss1/raid-commander/issues/14.
- Depends on: completed T8 boss telegraphs/reactive party AI and T9 combat HUD, plus existing combat stats and manual movement ownership.
- Target implementer: Codex Sol 6.1, High reasoning.
- Canonical specification: obsidian-vault/prompts/tasks/T10-awareness-and-decision-log.md.
- The requester confirmed the implementation scope and subsequently approved the T10 review checkpoint on 2026-10-22.

## Objective
Add an integer Awareness stat from 1 through 10 to party members. While inside a pending telegraph, an eligible member rolls immediately and then every 0.5 seconds until a successful check commits it to escape. Each point grants a 10% success chance per check. Present Awareness checks and their related escape outcomes in a Decisions tab beside Combat within the existing combat-log panel.

## Requester decisions (2026-10-22)
- Use T10 and target Codex Sol 6.1 with High reasoning.
- Awareness is an integer from 1 to 10; chance is Awareness × 10%, including guaranteed check success at 10.
- Roll immediately upon entry, then every 0.5 seconds of game time after failures.
- On success, commit to escape and stop rolling until safe; retry pathfinding if blocked.
- Manual movement takes priority. Suspend rolls during manual orders and restart entry timing afterward.
- Preserve existing preventive avoidance. Awareness gates escape from inside a telegraph, not ordinary path safety.
- Overlaps produce one check per member per interval, not one per telegraph; success attempts escape from all overlapping hazards.
- All four party members start at Awareness 5. Configure through existing stats assets only; no new player-facing stat display/editor.
- Decisions contains Awareness checks plus escape-start, blocked-path and manual-suppression outcomes. Unrelated AI decisions are excluded.
- Combat and Decisions have independent bounded histories of 1,000 entries each, continue recording while hidden, and preserve separate scroll positions. Encounter reset clears both. Fresh Play defaults to Combat.
- The existing Editor debug view temporarily replaces the tabs and restores the previously selected tab afterward.

## Refined execution plan
1. Extend existing combat stats and runtime access with validated Awareness, preserving authored balance.
2. Add a bounded Awareness timing/commitment state to party hazard reactions while preserving manual priority, preventive avoidance and independent combat.
3. Publish immutable decision results into an optional encounter-local bounded recorder.
4. Extend the existing uGUI/TMP log presentation with exclusive Combat/Decisions tabs and independent scroll/follow state.
5. Wire the saved arena and repeatable setup, verify deterministic logic and actual Play-mode/UI behavior, then update affected system notes.

## Existing integration baseline
Reinspect current source, scene, task and issue state before editing; notes can retain historical verification details.
- `CombatStatsDefinition` holds per-unit stats as ScriptableObject data; `CombatUnit` exposes runtime values. Add Awareness here rather than introducing a parallel character-stat framework.
- `PartyRoleBrain` owns positioning decisions and independently attempts valid attacks/heals. Its current priority is death/inactive encounter, manual movement, automatic escape, non-Tank retreat toward Tank, then normal positioning. Replace unconditional escape admission with Awareness checks.
- `ReactToEncounter` currently detects center containment, commits to escape through an agent-clearance margin, and retries escape search every 0.2 seconds. Preserve useful existing escape behavior after Awareness succeeds.
- `GroundHazards` contains pending footprints only, with `IsUnsafe`, `PathIsSafe` and `TryEscape`. `HazardFootprint` supplies the authoritative clipped XZ geometry. Red impact visuals are not pending hazards. Reuse these owners; do not detect danger by renderer colors or new trigger volumes.
- `GroundHazards` currently maps owners to geometry without log-ready cast metadata. Extend registration/query data narrowly if needed to identify the pending cast(s) represented by a decision, preserving existing callers and geometry ownership.
- `UnitMovement` owns navigation command acceptance, completion, cancellation and failure. Reactions submit autonomous commands and must never acquire manual ownership or cancel a newer player command.
- `CombatLogService` already owns bounded combat history, entry/reset notifications and encounter-local sequence values. Preserve its combat semantics. Decision outcomes are not damage/heal/cast events.
- `CombatHud` builds the existing uGUI/TMP log, party frames and Editor debug replacement. Reuse its visual language, layout, pointer blocking and event-driven presentation. Consult applicable Unity UI skills before writing UI code.
- Integrate with `CombatSceneSetup`, `PartyAiSceneSetup`, `BossEncounterSceneSetup`, `CombatHudSceneSetup` and the arena-generation path where relevant. Apply intended scene/asset changes through supported Unity Editor tooling.
- Planning-time unrelated work includes customized WallOfFlames, Boss/Tank/Healer/Dps combat assets and recovery-scene artifacts. Preserve those edits and any dirty Editor state. Adding Awareness must not overwrite health, damage, timing, range or attack-speed tuning.

## Work package 1: Awareness stat
- Add serialized integer Awareness with Inspector range 1–10 and runtime validation. Clamp out-of-range values; do not rely solely on Inspector attributes.
- Initialize the four existing party stat assets to 5 without replacing their identities or changing other stats. A missing stats reference uses Awareness 5. Subsequent setup preserves valid authored Awareness values.
- Expose validated Awareness through the existing runtime unit/stat access pattern. Read the current value for each check so future Inspector tuning is not masked by a stale cached chance.
- Use a uniform integer roll from 1 through 10 inclusive; succeed when roll <= Awareness. Log the actual value used and the resulting chance. Awareness 10 guarantees detection success, not a reachable exit or escape before impact.
- Keep the random source replaceable or controllable for focused tests without introducing a general randomness framework or changing boss-target randomness. Do not reseed Unity's shared random state to test this feature.
- The boss does not perform Awareness checks. Do not add progression, equipment modifiers, buffs or a generic stat system.

## Work package 2: timed checks and escape commitment
- Eligible members are living, active party members in an active encounter without a manual movement override. Death, inactive encounter, disable and reset clear pending checks and escape commitment.
- Detect entry using the member's ground center inside/on a pending footprint, consistent with current hazard containment. A telegraph appearing around a stationary member also counts as entry.
- Perform the initial check on the first eligible decision update that detects entry. After failure, schedule the next check for 0.5 game-time seconds later; allow ordinary frame/decision-cadence tolerance. Keep this timer independent of position-search and attack/heal timers.
- Pause game time means no repeated checks. A delayed frame must not generate a burst of catch-up rolls. Exit before a check cancels its timer; later re-entry starts with an immediate check.
- Treat all currently containing footprints as one exposure for that member. Adding/removing one overlapping footprint while the member remains inside another does not grant extra immediate checks or multiply the cadence.
- A failed check does not issue an escape command. Continue existing ordinary role/combat behavior and preventive safety rules. Role-driven movement may incidentally leave a footprint; failure is not a forced stand-still command.
- A success commits the member to escaping pending hazards. Stop further checks while committed, retain existing bounded pathfinding retries, and seek a reachable endpoint clear of all pending footprints with the existing agent-clearance margin. An unavailable path retains commitment and reports `NoSafeExit`; do not require another success to retry.
- Continue commitment until clear using the existing clearance-based escape release, or until all relevant danger disappears. Once released, subsequent genuine entry starts a new check cycle. New hazards encountered while committed are handled by the existing aggregate safe-exit search without extra rolls.
- Preserve preventive path checks before and after detection: ordinary autonomous destinations/routes avoid pending footprints, and unsafe normal paths may be stopped as they are today. Do not let those checks invoke unconditional escape and bypass the Awareness gate.
- Player orders interrupt autonomous escape immediately through existing movement ownership. Clear the previous Awareness commitment and suspend checks during manual movement. When that override ends, reevaluate as a fresh entry: if still inside, roll immediately, consistent with the selected entry timing; otherwise remain idle.
- Removing/canceling/resolving a telegraph stops its eligibility. Impact-only visuals never trigger rolls. Reuse authoritative pending-hazard state and handle update ordering without attributing checks to an already resolved cast.
- Preserve Tank/chase reactions, healing priorities, selection, valid combat during movement, mutually passable units, 3D XZ movement and NavMesh elevation. Do not freeze unrelated AI decision-making to implement the 0.5-second check interval.

## Work package 3: encounter decision history
- Add immutable decision entries and an optional encounter-local recorder with read-only history and entry/reset notifications. Capacity defaults to 1,000; evict oldest entries. Keep history and presentation bounded.
- Every actual Awareness check records encounter-local sequence, elapsed session timestamp, member stable ID/name, containing telegraph/cast identity or identities, Awareness, probability, rolled integer and success/failure.
- Identify simultaneous casts distinctly even if they use the same spell. Capture metadata at recording time so expired hazards or destroyed sources do not change historical text. Do not build replay/persistence infrastructure.
- Record related outcomes: escape command accepted/started, no safe path or rejected escape command, and manual suppression/interruption while exposed. A successful roll and a successful movement submission are distinct facts; never label detection as a completed dodge.
- Suppression records have no fabricated roll/chance outcome. Log once when entering an exposed-but-manually-controlled state or when a manual order interrupts committed escape, not every frame. A new exposure can produce a new suppression record.
- Deduplicate unchanged blocked-path states across retries. Record renewed escape start when movement actually begins after blockage; ordinary repathing of an already running escape is not another start. Avoid filling the history with repeated status lines at the 0.2-second search cadence.
- Record no targeting, routine healing/attack choices or ordinary positioning decisions. Combat results stay in Combat; decision results stay in Decisions.
- Both recorders continue collecting while their tabs or the whole log region are hidden. Logging and UI availability must not change randomness, decision outcomes, movement or combat.
- Use the existing encounter reset flow to clear both histories, reset their sequences/timestamp origins and clear decision timing/commitments. Preserve the combat timestamp convention and align session origins for understandable cross-tab comparison; the 0.5-second gameplay cadence still uses scaled game time.
- Manage subscriptions through enable/disable/rebind/destruction without duplicates or leaked scene references. Avoid a global manager, generic event bus or a second combat-resolution pipeline.

## Work package 4: Combat and Decisions tabs
- Add visible `Combat` and `Decisions` tabs inside the current log panel. Exactly one log view is visible/interactable. Match existing panel, typography, colors, row formatting, masking and scrolling; clearly distinguish the active tab without relying on color alone.
- Fresh Play selects Combat. Tab switching changes presentation only and never resets either history or affects gameplay. Encounter reset clears both histories and resumes following; keep the selected tab for the current session.
- Maintain independent scroll/follow state per tab. Follow incoming entries only when that tab was at its bottom; scrolling up pauses follow. Switching away and back preserves scrollback, while a tab following the bottom shows its latest entries on return.
- Handle oldest-entry eviction while scrolled up without jumping to newest. Show current bounded history after reopening/re-enable and an understandable empty state when no entries exist.
- Format checks readably, for example: `00:12.5 Tank | Wall of Flames [cast 3] | Awareness 5 | 50% | roll 8/10 | Failed`. Outcome rows clearly distinguish escape started, no safe exit and manual suppression. Long identity lists/text remain contained through wrapping/clipping/scrolling.
- Keep the existing Editor menu debug replacement: enabling it hides both tab views and tab controls, preserves selected tab and both scroll states, and disabling it restores them. Preserve debug actions and boss protection. Player builds include both logs/tabs and exclude Editor debug UI.
- Tab clicks must not select/clear world units; right-click over the panel must not order movement; wheel scrolling must not zoom the camera. Preserve keyboard controls and transparent center input.
- Retain T9's landscape support and central safe rectangle: inspect 1280×720, 1920×1080, 2560×1440 and 3440×1440. Keep essential log/tab text readable, panels separated and content inside the existing region.

## Work package 5: scene, documentation and verification
- Wire the optional decision recorder, hazard identity metadata and HUD references into the saved arena and repeatable setup. Preserve scene/asset identity, geometry, navigation bakes and authored balance. Do not regenerate the arena blindly.
- Reapplying setup must preserve valid Awareness tuning and must not duplicate services, controls, subscriptions or EventSystems. Verify both the current-scene setup and relevant generator integration.
- Separate stat data, timing/random decision logic, movement execution, history recording and UI responsibilities. Prefer small feature-owned helpers/components only where needed; avoid expanding `CombatHud` or `PartyRoleBrain` into unrelated frameworks.
- Add deterministic focused checks for probability boundaries, scheduling and lifecycle transitions; use injected/scripted roll sequences rather than flaky statistical pass criteria. Use temporary runtime stat copies for fixtures and restore test state.
- Reuse existing Play-mode runners for relevant integration/regression coverage. Existing T8 unconditional-escape fixtures must explicitly use Awareness 10 or controlled successful rolls; do not remove escape checks to make them pass. Add failure-before-success coverage for the new production behavior.
- Verify advancing Play-mode frames, compilation/Console results, actual Game views and supported player build/runtime behavior. A build alone is not proof of player tab interaction; report unavailable runtime checks explicitly.
- Update `systems/autonomous-party-ai.md`, `systems/combat-and-logging.md`, `systems/combat-hud.md` and `systems/boss-abilities-and-hazards.md` for actual implementation. Maintain required versioned system-note structures and newest-first revision tables. Update README, structure and checklist only where behavior/status changes warrant it.
- Exit temporary Play/test state, restore fixtures and preserve user-owned edits. Leave implementation uncommitted for review with accurate verification evidence and gaps.

## Acceptance and verification matrix
1. All four party assets start at 5; integer bounds, missing-stats fallback, current-value reads and preservation on repeated setup work without unrelated balance changes.
2. Controlled rolls prove 1 succeeds only on 1, 5 succeeds on 1–5, and 10 always succeeds; logged values exactly match the decision. Test invalid stat values at both bounds.
3. Entry rolls immediately; failures retry at 0.5 seconds within documented cadence tolerance. Verify stationary telegraph appearance, pause, slow frames, exit/re-entry and no catch-up bursts.
4. Two overlapping telegraphs produce one member check per interval. Changes to the overlap set do not reset timing; success uses aggregate safe-exit geometry and correct cast metadata.
5. Success stops further checks and continues escape/retries until safe. Unavailable navigation or blocked exits cannot masquerade as success; recovery needs no new roll and unchanged blockage does not spam history.
6. Failed checks do not trigger autonomous escape through another code path. Existing preventive avoidance and incidental normal role movement remain intact; all four roles use the same Awareness rules.
7. Manual orders interrupt escape and suppress all checks. Logs truthfully report suppression without fictional rolls. Completion/cancellation/failure reevaluates immediately if still exposed and does not reuse an earlier success.
8. Telegraph cancellation/impact, death, disable, boss death and reset clear stale state; impact visuals never trigger checks. Reset leaves no stale log entries or duplicate subscriptions.
9. Combat and Decisions remain separate and bounded at 1,000 each. All required check fields/outcomes are accurate, immutable and recorded while hidden. Missing/disabled logging and UI do not affect gameplay outcomes.
10. Tabs switch exclusively, preserve independent scroll/follow behavior through arrivals/eviction and default to Combat on fresh Play. Reset clears both; debug replacement/restoration preserves the selected tab and state.
11. Both tabs remain readable and contained at all four supported resolutions and live resize. Pointer isolation, party selection, camera controls, world-bar visibility and the existing HUD remain correct.
12. Existing attacks/heals, manual movement ownership, chase/retreat, preventive navigation, boss cast/damage and combat history semantics pass relevant regressions. No boss Awareness behavior is introduced.
13. Saved wiring and repeatable setup work without duplicates or user-data loss. Compilation, focused runtime checks, visual inspection and player-build/runtime evidence are reported honestly.

## Exclusions and completion boundary
No progression, gear, stat modifiers, Awareness HUD/editor, new abilities, new telegraph shapes, boss detection, per-telegraph roll multiplication, removal of preventive avoidance, all-AI decision tracing, log persistence/export/search/filtering, replay, general telemetry framework, combat rebalance, damage meters, encounter results or arena redesign.

The planning handoff required this specification and its unique issue before gameplay work. The requester subsequently asked to implement T10. Saved integration, focused checks, visual verification and vault updates formed the review checkpoint, with gaps disclosed below. The requester approved that checkpoint on 2026-10-22.

## Implementation review (2026-10-22)
- Implemented Awareness on the four party stats assets, one check stream per member, committed escape with path retries, manual suppression, cast identity capture, separate bounded Decisions history, and Combat/Decisions tabs. The saved arena and repeatable setup wire one recorder to four role brains and the HUD; repeat setup preserved authored stats and avoided duplicate services or UI.
- `AwarenessVerification` passed in advancing Play mode with deterministic probability/timing checks, failure before success, overlapping casts, four roles, blocked-path recovery, manual suppression, bounded history, tab/scroll/debug behavior and reset. The existing `PartyAiVerification` regression runner also passed. Game views were inspected at 1280×720 and 3440×1440, and `CombatHudVerification.CheckLayout()` passed at 1280×720, 1920×1080, 2560×1440 and 3440×1440.
- A Windows64 player build succeeded with zero errors before the final scroll-follow correction. Later Unity Pipeline compile/build requests timed out although the Editor still reported ready; the final correction was verified by the T10 Play-mode runner but was not rebuilt. Standalone tab interaction remains unverified. Brief player startup checks found no C# exceptions but did report NavMesh agent creation warnings; agent attachment was confirmed in Editor Play mode when the surface enabled.
- The requester approved these T10 changes on 2026-10-22. Existing requester-authored combat-stat edits and recovery-scene artifacts are excluded from the T10 commit.

## Implementation prompt for Codex Sol 6.1 High
Implement T10 from this canonical specification using the implementing-game-development-tasks skill and applicable Unity UI/Editor guidance. Reinspect repository, lifecycle and source; preserve unrelated work and authored tuning. Recorded requester decisions are resolved; ask only about meaningful new conflicts and wait before dependent work. Add integer Awareness with immediate/0.5-second checks, aggregate overlap handling, committed escape, manual suppression and preserved preventive avoidance. Keep decision logging observational and separate from combat results. Reuse the existing HUD for exclusive Combat/Decisions tabs, independent bounded history/scroll state and the Editor debug replacement. Complete saved scene wiring, repeatable setup, deterministic checks, focused Play-mode and visual verification, and accurate system documentation. Stop uncommitted at awaiting review with results, scope and explicit verification gaps. Do not commit, push or complete the issue without task-specific approval.
