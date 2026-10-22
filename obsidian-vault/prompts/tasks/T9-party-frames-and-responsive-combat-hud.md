# T9 — Party frames and responsive combat HUD

## Tracking and handoff
- Status: implemented; requester approved T9 on 2026-10-22. Clarification was resolved on 2026-10-21.
- GitHub issue: https://github.com/Regiss1/raid-commander/issues/13.
- Depends on: completed T8 encounter integration, T5.1 reactive health, and existing four-member selection/combat integration.
- Target implementer: Codex Sol 6.1, High reasoning.
- Canonical specification: obsidian-vault/prompts/tasks/T9-party-frames-and-responsive-combat-hud.md.
- The requester explicitly requested implementation and approved T9 for commit, push and completion on 2026-10-22.

## Objective
Create a responsive combat HUD inspired by the supplied sketch and WoW party frames. The primary deliverable is four selectable party frames with health driven by existing combat systems. Include a live combat log, an Editor-only debug replacement, and nonfunctional damage-meter/group-ability mocks while keeping the arena center unobstructed.

## Requester decisions (2026-10-21)
- Health-only party frames: colored identity icon, role name, current/max HP and selection highlight. Retain existing Tank blue, Healer green, Melee DPS orange and Ranged DPS cyan colors.
- Anchored responsive overlays, with the arena rendering behind them; no dragging or saved panel positions.
- Landscape desktop, minimum 1280×720; include 1080p, 1440p and ultrawide. Portrait/smaller layouts are outside acceptance.
- Editor menu toggle, off by default, directly replaces the ordinary combat log with debug controls/history. Remove F8 access; omit debug UI in player builds.
- Live combat log with scrollback; automatically follow new events only when already at the bottom.
- Keep world-space party bars and boss HUD. Add a configurable Input System binding, default V, to hide/show party world-space bars. Editor configuration is sufficient; no in-game rebinding screen.
- Damage meters and WIP group abilities are visual mocks only.

## Visual references and interpretation
- [Image 1: layout sketch](../references/T9/hud-layout.png).
- [Image 2: WoW party-frame inspiration](../references/T9/party-frame-inspiration.png).
- Copies are retained in the vault so future implementation does not depend on temporary clipboard paths. These are design references, not game assets or additional instructions.
- Boss health top-center; damage-meter mock upper-left; log below it; four party frames stacked right; group-ability mock bottom-center.
- Use compact stacked frames and readable horizontal health presentation as inspiration. Use simple original icons/panels, not copied WoW portraits/textures, resource bars, levels or buffs.
- Current role colors override the sketch palette. Frame order is Tank, Healer, Melee DPS, Ranged DPS, matching hotkeys 1–4. Reference HP values are illustrative.
- The arena outline is a composition guide, not a new cropped viewport. Keep the 3D camera rendering the full view and preserve arena geometry.

## Refined execution plan
1. Extend existing uGUI/TMP presentation with coordinated layout and four reactive party frames.
2. Integrate the selection owner and prevent HUD pointer events from issuing world commands.
3. Present bounded existing combat history and fit Editor debug controls into the same log region.
4. Add inert labeled mocks and the authored V action for world-bar visibility.
5. Wire saved scene/repeatable setup, verify behavior and actual Game views, then document implemented systems.

## Existing integration baseline
Reinspect current source, scene and task/issue state before editing.
- `Assets/_Game/Scripts/UI/HealthBarBinding.cs` subscribes to `CombatUnit.HealthChanged` and formats current/max values. Reuse authoritative state; do not poll health or derive it from logs.
- `CombatUnit` owns health/death/cap changes. `CombatSystem` and existing debug actions own resolution/reset. Presentation must not calculate combat outcomes.
- `ScreenBossHealthBar` owns the current boss HUD; preserve its reactive percentage, current/max values and name, coordinating layout instead of adding a duplicate.
- `WorldHealthBar` owns position/orientation/zoom compensation. Visibility must affect presentation only.
- `UnitSelectionController.Select` and `SelectionChanged` own single selection. Use them for clicks/highlights and retain rings/hotkeys.
- `CombatLogService` owns bounded history (default 1,000 entries) and `EntryRecorded`. `ResetSession` currently has no dedicated reset notification; a small presentation-safe notification may be added if needed. Do not create another combat event/history pipeline.
- `CombatDebugPanel` currently uses IMGUI, starts visible, handles F8 and displays a fallback button when hidden. Replace that access behavior completely.
- Coordinate pointer gating across `UnitSelectionController`, `PlayerMovementInput` and `OrthographicArenaCamera`.
- Integrate with `CombatSceneSetup`, health/selection setup and the generator path. Consult applicable Unity UI skills before implementation; reuse existing uGUI/TMP assets and conventions.
- Preserve customized geometry, navigation, balance and unrelated changes. Planning-time unrelated work includes BossCombatStats, DpsCombatStats, HealerCombatStats edits and recovery-scene artifacts; recheck at implementation time.

## Work package 1: reactive party frames (primary deliverable)
- Bind exactly four frames to existing party members in stable role order. Do not create units or bind mock data to live frames.
- Each entire frame is clickable and shows a simple colored identity icon, role name, horizontal health fill and current / maximum HP. Use current role colors for fill/accent, a contrasting missing-health region and readable text.
- Provide a distinct selection border; role labels and border must convey identity/selection without color alone.
- Fill and values update together for damage, healing, death, maximum-health changes and reset. Initialize immediately, refresh on re-enable/rebind and handle zero maximum safely.
- Keep dead members in place with zero health and a clear Dead state. Preserve existing selection eligibility; no new resurrection/death-selection rules.
- Left-click calls existing single-selection API. World selection/hotkeys update the same highlight; clearing selection clears it; clicking the selected frame retains selection.
- Release subscriptions on disable/destruction/rebinding. No duplicate handlers, health polling or combat logic in frame presentation.

## Work package 2: responsive layout and input isolation
- Anchor panels at screen edges with coordinated margins/sizing. Preserve the sketch placement across supported landscape resolutions; keep all four frames, boss HUD and ability slots on-screen.
- Define a central safe rectangle shared by normal/debug layouts. Testable minimum: the middle 50% of screen width by middle 50% of screen height contains no screen-HUD panels, backgrounds or HUD raycast targets. Existing world bars/telegraphs are not screen-HUD obstructions.
- No panel overlap or essential-text clipping. Keep essential role/HP/log text at least 14 rendered screen pixels at 1280×720. Size HP fields for current authored maxima and comma-separated whole numbers.
- Clip/scroll long log/debug content inside its panel, never expanding it across the center.
- Transparent canvas/root and arena center pass pointer input through. Only visible panel regions consume it; inactive debug UI must not intercept input.
- HUD left-click must not select/clear world units behind it; frame selection occurs once. Right-click over HUD must not issue movement or markers. Wheel scrolling over log/debug must not zoom the camera.
- Preserve world controls elsewhere and keyboard role hotkeys; hovering a panel alone must not block keyboard controls. Respect actual UI keyboard focus where applicable.
- Verify live resolution changes and both log/debug modes. Do not build draggable layout, a new viewport or camera-framing system.

## Work package 3: live combat log and Editor debug replacement
- Show existing history chronologically with timestamp, readable source/target, event type, applicable amount and spell identity. Preserve normal/protected damage semantics; never recompute results from HP deltas.
- On initial display/reopening show current history. Follow new events only at bottom. Scrolling up pauses following; new events do not pull the reader down. Returning to bottom resumes following.
- Respect bounded history and bounded UI work/storage. Handle eviction while scrolled up without jumping to newest. Reset clears stale displayed entries and resumes following the new session.
- Add checked menu item `Raid Commander > UI > Show Combat Debug`, default off on a fresh Editor session. It can be set before Play and toggled during Play; use Editor state, not a production scene override/player preference.
- Enabling replaces the ordinary log in the same left-side region. Exactly one view is visible/interactable; internal scrolling keeps every debug control accessible without covering the center.
- Retain all existing debug actions, reset, boss protection, feedback, party AI/targets, boss state/cast/setup diagnostics and history through existing owners.
- Swapping presentation must not reset combat/history or silently change an active debug option. Turning it off restores the ordinary live log.
- Remove F8 and the old fallback Show Combat Log/debug button.
- Compile Editor menu/state access only for Editor. Player builds must not create/render debug UI or expose debug UI input; show the ordinary log. Existing underlying APIs need not be removed.

## Work package 4: mocks and configurable world-bar visibility
- Damage Meters: clearly labeled Mock panel with static sample ranked bars using party colors. No aggregation, DPS timer, live sorting or combat subscriptions; sample values cannot masquerade as live results.
- WIP: Group Abilities: inert slots following the sketch: Group Heal, DPS Boost, two WIP slots, Special, Class Ability 1–4. Clearly disabled/nonfunctional; no action, resource, cooldown or hotkey gameplay.
- Keep sample content independent of live party-frame state. No speculative action-bar framework or new ability definitions.
- Add an Editor-configurable Input System action/binding `Toggle Party World Health Bars`, default `<Keyboard>/v`. Use an authored binding rather than an unchangeable key check.
- Bars start visible at Play entry. The binding toggles all four together; keep that visibility choice across encounter resets for the current Play session. A new Play session starts visible. No persistent settings UI.
- Only world-space party health presentation changes. Party frames, boss HUD, units, combat and selection rings remain active. Revealed bars immediately reflect damage/healing/death/cap changes/reset that occurred while hidden.
- Review correction (2026-10-22): all four saved world bars use `screenHeight = 10` at a 720-pixel-tall camera view. Their projected height grows with viewport height while retaining zoom compensation; the Tank value of 100 was an unsaved test parameter.
- Verify an alternate authored binding works and restore saved default V. This feature also works in player builds, independently of Editor debug access.

## Work package 5: scene, documentation and verification
- Apply intended scene/asset changes via supported Unity tooling to the saved arena. Preserve identities, authored geometry/stats and navigation bakes; do not regenerate the arena blindly.
- Extend repeatable setup to preserve/recreate the HUD and bindings without duplicate canvases, frames, EventSystems, subscriptions or debug panels.
- Keep layout, binding, log and visibility responsibilities small. Selection remains in Selection; combat state remains in Combat. Shared pointer gating is justified by actual consumers, not a general framework.
- Verify compilation and focused Play-mode behavior. Inspect actual Game views at 1280×720, 1920×1080, 2560×1440 and 3440×1440 with partial/dead health, selection and both log/debug modes.
- Verify player behavior/build exclusion with a supported local player build when available. Missing modules/toolchains are explicit gaps, not proof that source guards work at runtime.
- Create/update `obsidian-vault/systems/combat-hud.md` using the required system-note structure and revisions table. Update health-bars, team-member-selection, combat-and-logging and relevant README/checklist/structure notes only for actual implementation. Remove obsolete F8 instructions.
- Restore test bindings, fixtures, stats, camera/resolution settings; exit Play mode and save intended changes. Keep temporary test artifacts out of review scope.

## Acceptance and verification matrix
1. Four stable frames match role order/colors; icon, label, HP and selection remain legible.
2. Damage/heal/death/cap changes including zero/reset/rebind/re-enable keep fill and text correct independently of logging or debug visibility.
3. Frame/world/hotkey selection share one owner and synchronize highlight/ring; HUD background clicks do not change world selection.
4. HUD right-click issues no order/marker; log/debug scrolling does not zoom; the center remains fully usable for world controls.
5. All four target resolutions and live resizing keep panels on-screen, separated, readable and outside the central safe rectangle in both modes.
6. Log shows real events/spell identity, follows at bottom, preserves scrollback on arrivals, handles eviction and clears on reset.
7. Fresh Editor session defaults to normal log; menu swaps exclusively; all debug tools fit via scrolling. F8 and fallback access are gone.
8. Player build has ordinary log, party HUD and configurable V behavior, with no debug UI/access. Record runtime evidence or specific gaps.
9. Meter/ability mocks are labeled, inert and independent of combat calculations; no live meter/ability functionality exists.
10. V toggles only four world bars; hidden-state health changes refresh on reveal; reset preserves visibility; fresh Play starts visible; alternate binding works. All four bars use the corrected 10-pixel 720p reference size and remain readable as resolution increases.
11. Boss HUD, world selection/hotkeys, manual orders, AI combat, T8 hazards/abilities and authored stats are preserved.
12. Repeated setup adds no duplicates or scene damage. Compilation, focused checks and visual inspection pass; gaps are explicit.

## Exclusions and completion boundary
No live damage meters, group/class ability gameplay, portraits, resource bars, buffs, extra party members, multi-selection, draggable layouts, saved profiles, in-game rebinding, combat rebalance, encounter-results screen, arena redesign, full UI art pack, portrait/mobile or sub-1280×720 layout acceptance.

Implementation is present in the saved arena, HUD scripts, Editor setup and authored input asset. Focused Play-mode checks covered four frames, selection, health changes, reset, log scrolling/eviction, pointer isolation, debug swap and world-bar visibility. Layout checks and full-size Game view inspection passed in normal and debug modes at all four target resolutions. The final Unity Console showed zero errors and warnings, and a Windows64 player build succeeded with zero errors. Player runtime interactions and physical V/alternate-key presses remain verification gaps; synthetic keyboard input did not trigger the pre-existing selection hotkey either.

The 2026-10-22 review fix scales world-bar presentation with camera viewport height and saves `screenHeight = 10` on Tank, Healer, Melee DPS and Ranged DPS. Play-mode projected-height checks were approximately 9–11 pixels at 1280x720, 14–17 at 1920x1080, and 19–22 at both 2560x1440 and 3440x1440; full-size Game views at 720p, 1440p and ultrawide were inspected. Direct hide/show verification affected exactly four bars in each direction. Unity recompiled the correction without errors, and the updated Windows64 player build succeeded with zero errors (504 shader/build warnings). Player runtime interaction remains unverified.

## Implementation prompt for Codex Sol 6.1 High
Implement T9 from this canonical specification using the implementing-game-development-tasks skill and applicable Unity UI/Editor guidance. Reinspect repository, lifecycle and source; preserve unrelated work. Recorded requester decisions are resolved; ask only about meaningful new conflicts. Reuse existing health, selection, combat/log and debug owners. Prioritize reactive selectable frames, then complete responsive layout, pointer isolation, log/debug replacement, inert mocks and configurable V visibility. Complete saved scene wiring, repeatable setup, focused runtime/visual verification and accurate vault documentation. Stop uncommitted at awaiting review with results, reviewed scope and explicit verification gaps. Do not commit, push or complete the issue without task-specific approval.
