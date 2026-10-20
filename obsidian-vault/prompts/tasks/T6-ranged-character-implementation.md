# Task 6 — Add Ranged DPS, Basic Ranged Attacks & Selection Hotkey

## Tracking

- GitHub issue: https://github.com/Regiss1/raid-commander/issues/8
- Lifecycle: implemented and verified; requester approved T6 and T6.1 for commit, push and issue completion on 2026-10-20.

## Requester clarification

- 2026-10-20: The debug utility uses the boss as its only attack target for now, consistent with the existing combat actions. T6 adds no hostile target selector or world-click hostile selection. Existing healing and damage debug actions retain their established targets.
- 2026-10-20: Confirmed initial Ranged DPS values: 100 health, 4.5 movement speed, 20 damage, 18-unit XZ range. Attacks resolve immediately with a brief cyan tracer and existing range/faction/death validation, without a line-of-sight check.

## Implementation plan

- Add a cyan Ranged DPS alongside the existing party, with a distinct role, stable encounter ID, existing movement executor and reactive world-space health bar. Preserve existing scene customization.
- Extend existing Input System selection with hotkey `4`; preserve hotkeys `1`–`3`, mouse picking, and manual movement ownership.
- Add reusable immediate ranged attacks to `CombatSystem`, using configured damage/range and the existing damage/event pipeline. Publish `RangedDamage` followed by `UnitDied` for lethal hits.
- Add a boss-targeted ranged debug action, feedback, log name mapping and reset integration. A separate event subscriber presents a short cyan tracer without calculating damage.
- Extend arena/setup utilities idempotently. Verify the saved scene in Play mode: four units, all selection methods, ranged movement/passability/manual priority, range/faction/death rejection, effective damage/event metadata, health/reset, tracer lifetime, existing combat actions, and Console errors. Inspect the Game view at camera zoom limits.
- Preserve the task exclusions, including no hostile selection UI, autonomous combat, automatic attacks, right-click attacks, line-of-sight checks, or projectile simulation.

## Objective

Expand the existing party by introducing a **Ranged DPS** character, bringing the party to four members: Tank, Healer, Melee DPS, and Ranged DPS.

Implement a reusable basic ranged attack mechanic and integrate it with the existing combat debug tooling.

The implementation must follow the established architecture of our Unity 6, C#, 3D top-down raid simulator.

Before implementing, inspect the existing project and reuse established systems and conventions.

## 1. Ranged DPS character

- Create a Ranged DPS unit using the existing character architecture.
- Give the character a visually distinguishable placeholder model or color (Cyan)
- Assign the friendly faction and Ranged DPS role.
- Configure its health, movement speed, attack damage, and attack range.
- Spawn it alongside the existing party members.
- Reuse existing movement, health, and combat components.

## 2. Unit selection hotkey

Update the selection system to support four party members.

HotkeyCharacter`1`Tank`2`Healer`3`Melee DPS`4`Ranged DPS

Requirements:

- Pressing `4` selects the Ranged DPS.
- Mouse selection works normally.
- The existing selection indicator works correctly.
- Switching between characters preserves existing functionality.
- Reuse the established Unity Input System implementation.

## 3. Basic ranged attack system

Implement a reusable ranged attack mechanic.

Requirements:

- A ranged attack targets a valid hostile unit.
- Validate the configured attack range using the existing 3D positioning conventions.
- The attacker must face its target when attacking.
- Damage must be configurable through the existing combat stats architecture.
- Attacks must respect unit death states.
- Dead units cannot attack or receive further damage.
- Reuse the existing damage resolution system.
- Keep attack logic independent of visual effects.

For now, ranged attacks should resolve damage immediately when triggered. Physics-based projectiles are not required.

The implementation should support future expansion into projectile attacks, attack intervals, and autonomous combat without implementing those features now.

## 4. Combat debug tooling integration

Extend the existing combat debug tooling introduced in Task 5.

Requirements:

- Include the Ranged DPS in the available combat units.
- Allow testers to trigger a basic ranged attack from the Ranged DPS against the boss, the debug utility's only attack target for now, through the existing debug interface.
- Apply the ranged attack's configured damage through the normal combat resolution pipeline.
- Validate attack range and target eligibility.
- Provide appropriate feedback when a debug attack fails validation.
- Ensure health bars update correctly after ranged damage.
- Ensure ranged damage and resulting deaths generate the appropriate combat log events.
- Preserve all existing melee attack, damage, and healing debug functionality.

**Do not implement right-click attack commands, automatic attacks, or autonomous targeting in this task.**

The debug tooling remains the primary method for testing combat functionality until those systems are introduced in subsequent tasks.

## 5. Combat events and logging

Integrate ranged attacks with the existing event-driven combat architecture.

Requirements:

- Publish a resolved combat event for successful ranged attacks.
- Introduce `RangedDamage` to `CombatEventType` if no appropriate ranged event exists.
- Include source ID, target ID, effective damage, and existing combat event metadata.
- Ensure events appear correctly in the combat log debug panel.
- Preserve existing `UnitDied` event handling.
- Do not duplicate the combat logging infrastructure.

## 6. Visual feedback

Provide minimal visual feedback for ranged attacks triggered through the debug tooling.

Requirements:

- The attacker faces its target.
- Display a simple tracer or projectile-like visual between attacker and target.
- Keep visual effects independent of combat calculations.
- Use Unity primitives or other existing project assets.

Elaborate animations and physics-based projectiles are not required.

## 7. Health bar integration

- Add a world-space health bar above the Ranged DPS character.
- Reuse the reactive health system implemented in Task 5.1.
- Ensure damage, healing, death, and health resets update the bar correctly.
- Preserve the boss's screen-space health bar.
- Follow the existing 2-pixel text outline convention.

## Acceptance criteria

1. Four party members are present in the prototype arena.
2. The Ranged DPS is visually distinguishable.
3. Hotkey `4` selects the Ranged DPS.
4. Mouse selection works correctly.
5. Existing hotkeys `1`, `2`, and `3` continue working.
6. The Ranged DPS supports existing movement commands.
7. The Ranged DPS can perform a basic ranged attack through the combat debug tooling.
8. Attack range and target validity are correctly checked.
9. Ranged attacks apply damage through the existing combat system.
10. Ranged attacks generate appropriate combat log entries.
11. Health bars update correctly following damage.
12. Basic ranged attack visual feedback works.
13. Existing debug combat functionality remains operational.
14. No Unity Console errors occur.

## Out of scope

- Right-click attack commands.
- Autonomous targeting or combat AI.
- Automatic attack intervals or attack loops.
- New spells or abilities.
- Physics-based projectile simulation.
- Advanced combat animations.
- Changes to boss mechanics.

## Deliverables

Implement the Ranged DPS character, ranged attack functionality, selection hotkey, debug tooling integration, and appropriate tests.

Summarize modified files, architectural decisions, and required Unity Editor configuration.

## Implementation and verification — 2026-10-20

- Saved `RaidArena.unity` now contains Tank, Healer, Melee DPS (`DPS`), cyan Ranged DPS and Raid Boss. Existing IDs 1–4 are preserved; Ranged DPS uses ID 5. Its stats asset supplies 100 health, 20 damage and 18-unit ranged range; its existing-style NavMeshAgent uses speed 4.5.
- Hotkey `4`, mouse selection, the selection ring and existing manual movement support the fourth unit. `PartyRole.RangedDps` and `CombatEventType.RangedDamage` are appended to preserve existing serialized enum values.
- `CombatSystem.TryRangedAttack` validates living opposing units and XZ range, faces the target and resolves damage immediately through `ApplyDamage`. An independent `RangedAttackTracer` observes resolved events and displays a cyan LineRenderer for 0.2 seconds.
- The ranged debug button targets only the boss. Invalid/dead/out-of-range requests report feedback and produce no combat outcome. The panel resolves the new source's name, and reset includes all five units.
- Reused existing reactive health presentation for the fourth world bar with a dark cyan role outline. Boss HUD and outlined text remain intact.
- `RangedDpsSceneSetup` updates the existing arena through the existing selection/movement/combat setup owners; the full arena generator creates the fourth unit before applying those owners. Repeated setup retained exactly five combat units, four selectable units/movers and five health bindings. The floor's pre-existing position/scale edits and existing asset GUIDs were preserved; navigation was rebaked for the current geometry.

| Acceptance criteria | Evidence / result |
|---|---|
| 1–2: four party members, distinct Ranged DPS | Saved-scene inspection and actual Game views confirm the cyan fourth unit. |
| 3–5: hotkeys and mouse selection | Synthetic Input System checks passed for `1`–`4`, mouse picking and selection-ring following. |
| 6: existing movement | Four-unit movement suite passed obstacle paths, passability, shared destinations, rejection, interruption, completion, cancellation and stall failure. Ranged checks passed real right-click input, selection switching and manual override priority. |
| 7–10: debug attacks, validation, damage and events | Combined combat/ranged suite passed exact range boundary, XZ/elevation behavior, faction/null/self/death rejection, immediate damage, source/target IDs, effective lethal damage, metadata and `RangedDamage` then `UnitDied`. |
| 11: health bars | Reactive health suite passed five screen resolutions; ranged checks passed boss damage updates and fourth-unit damage/healing/death/reset/movement updates. |
| 12: visual feedback | Runtime checks passed facing, tracer endpoints and expiry. Actual Game views inspected at 1280×720 with orthographic sizes 8, 20 and 30. Frame-count progression was confirmed through Pipeline `wait_for`. |
| 13: existing debug functionality | Direct Play-mode checks passed Tank melee, DPS melee, boss damage, healer healing, kill boss and reset, alongside the existing combat suite. |
| 14: Console | Compilation completed; final Editor inspection reported no compile failure, no missing scripts and zero current Console errors/warnings. Temporary diagnostic errors were resolved. |

### Reviewed file scope

- Runtime: selection role/controller; combat system, event enum and debug actions; debug panel; new ranged tracer.
- Editor/setup: arena generator, selection/movement/combat setup, health-bar role styling and new ranged setup utility.
- Assets: saved arena/NavMesh, Ranged DPS stats, cyan placeholder material and tracer material, plus new Unity metadata. Unity also serialized its default `m_ProjectSettingFolderPath: URPDefaultResources` field in `ProjectSettings/URPProjectSettings.asset`; no render-setting behavior was changed.
- Verification: existing movement/combat/reactive-health runners and new `RangedDpsVerification`.
- Documentation: README, vault index, development checklist, game-summary party scope, folder structure and the selection/movement/combat/health-bar system notes.
- Preserved pre-existing tracking documentation edits, T1–T5.1 task-file edits, and Obsidian settings. The arena's pre-existing floor edit remains part of the working tree. No files were staged, committed or pushed.

### Editor configuration and review boundary

All required scene/assets are applied and saved. The Editor is out of Play mode, the temporary Game view size has been removed, and diagnostic screenshots are in ignored `raid-commander-game/Logs/t6-*.png`. No further manual configuration is required. To repeat checks, run **Raid Commander > Verify Ranged DPS in Play Mode** and **Verify Movement in Play Mode** from Edit mode.

Code/document whitespace checks pass. The serialized scene retains Unity's generated empty-field trailing spaces. Existing selection-material values were restored after setup to avoid unrelated material changes.

Issue #8 stays open awaiting review. Approval must identify T6 before commit, push and issue completion; this implementation handoff performs none of those actions.

## Required scope revision — 2026-10-20

- [T6.1 — Ranged and healing line of sight](T6.1-ranged-and-healing-line-of-sight.md) records the requester-approved scope change: obstacle-height-independent blocking for ranged attacks and targeted healing, with debug feedback only.
- Revision issue: https://github.com/Regiss1/raid-commander/issues/9
- T6.1 is required before T6 can be accepted or completed. T6 is blocked by T6.1; its original scope, verification checkpoint, open issue state and `task:awaiting-review` lifecycle remain unchanged.
- T6.1 was initially recorded as planned; its separately requested implementation is now verified and awaiting its own task review. T6 remains awaiting review and blocked by T6.1 until the revision is resolved. Neither task is approved or completed by this handoff; the two tasks retain separate review, commit and issue-completion boundaries.

## Requester approval — 2026-10-20

The requester explicitly approved both tasks: “T6.1 and T6 are ok”. This authorizes separate task commits and a normal push to `feature/sprint-1/base-game`, followed by completion of T6.1 before T6. It does not authorize publishing unrelated local commits or unreviewed working-tree changes. The approved verification results above remain the implementation evidence; final publication and issue completion are tracked in GitHub issues #9 and #8.
