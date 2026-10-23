# Combat HUD v1.1

**Status:** implemented

## Overview

The screen-space combat HUD presents four selectable party frames, a live encounter log, Editor-only debug controls, and clearly inert meter and ability examples. Combat, health and selection remain owned by their existing systems.

## Behavior

- Four frames appear on the right in hotkey order: Tank, Healer, Melee DPS and Ranged DPS. Each has a numbered role icon, role name, colored health fill, current/maximum HP, Dead state and a gold selection border. Clicking a frame uses the existing single-unit selection and ring. World clicks and hotkeys update the same border.
- The boss's existing percentage/value/name HUD remains at the top. The meter mock sits above the left combat log; disabled group ability slots sit at the bottom. Panels stay outside the middle 50% of screen width and height at supported landscape sizes.
- The live log shows chronological resolved entries with elapsed time, source, target, event type, amount and spell ID where present. It follows new entries while at the bottom; scrolling up preserves position. History eviction stays within the combat service's bound, and reset clears the view.
- The left log region now has exclusive **Combat** and **Decisions** tabs. Decisions shows Awareness rolls with the current stat, chance, actual 1–10 roll and result, plus separate escape-start, blocked-exit and manual-suppression rows. Both 1,000-entry histories keep recording while hidden. Each tab retains its own scroll/follow position; reset clears both and keeps the selected tab for that Play session. Fresh Play starts on Combat.
- **Raid Commander > UI > Show Combat Debug** temporarily replaces both tabs with Editor-only controls and history in the same scrollable region. It restores the selected tab and its scroll position afterward. It defaults off each Editor session. The toggle does not reset combat or the boss-protection option. F8 has no HUD action. Player builds construct both ordinary log tabs and exclude debug controls.
- **V** is the authored Input System binding for toggling all four world-space party bars. The Input Action asset can be edited in the Editor. Hidden bars resume with current health when shown, and the choice lasts across encounter reset in the current Play session. A fresh Play session starts visible.
- Screen HUD panels consume pointer input. World selection, right-click movement and camera-wheel zoom remain available through the open arena center. No meter or ability mock issues gameplay requests.

## Implementation

- `CombatHud` builds uGUI/TMP presentation under the saved `Combat HUD` Canvas, using existing `HealthBarBinding` for each live frame. It observes `UnitSelectionController.SelectionChanged` and frame health notifications for border/Dead state; it does not resolve combat. Frame listeners and bindings release subscriptions on disable and rebind.
- The tab buttons and masked scroll views share the existing left panel and pointer blocking. `CombatHud` subscribes separately to the combat and decision recorders, renders immutable decision snapshots, and keeps independent follow and eviction offsets without moving decision ownership into UI. The active tab has an ASCII marker as well as a different fill.
- `CombatLogService.EntryRecorded` and `SessionReset` drive the log view. One TMP scroll content is rebuilt from the service's bounded history; no parallel history is stored. A full-opacity mask image hides content outside the viewport. The debug view uses a scrolling layout and calls existing `CombatDebugActions`.
- `UiPointerBlocker` raycasts current UI geometry before world input. The HUD's visible panel images and the boss bar are raycast targets, while the transparent Canvas/root and open center are not. Selection, movement and camera consumers share this query.
- `CombatHudSceneSetup` attaches references, a Canvas, a GraphicRaycaster, the Input System UI EventSystem and the authored action asset in the open arena. It removes the old IMGUI component, preserves balance/geometry, and can be applied repeatedly. The full combat/arena setup calls it too. The old class remains as an empty migration type for older serialized scenes.
- Editor menu state uses `SessionState`; debug construction and menu access are excluded by `UNITY_EDITOR` guards. The scene contains no active IMGUI debug UI.

## Source files

- `raid-commander-game/Assets/_Game/Scripts/UI/CombatHud.cs`
- `raid-commander-game/Assets/_Game/Scripts/UI/UiPointerBlocker.cs`
- `raid-commander-game/Assets/_Game/Scripts/UI/HealthBarBinding.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatLogService.cs`
- `raid-commander-game/Assets/_Game/Scripts/Units/DecisionLogService.cs`
- `raid-commander-game/Assets/_Game/Scripts/Units/DecisionLogEntry.cs`
- `raid-commander-game/Assets/_Game/Editor/CombatHudSceneSetup.cs`
- `raid-commander-game/Assets/_Game/Editor/CombatHudDebugMenu.cs`
- `raid-commander-game/Assets/_Game/Input/CombatHudControls.asset`
- `raid-commander-game/Assets/_Game/Tests/Editor/CombatHudVerification.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/AwarenessVerification.cs`
- `raid-commander-game/Assets/_Game/Scenes/RaidArena.unity`

## Current scope

The Decisions tab covers Awareness and related escape outcomes only; it does not trace all AI choices. The two tabs share the existing landscape panel and do not add a stat editor or persistent history.

The supported layout is landscape desktop at 1280×720 or larger, including 1920×1080, 2560×1440 and 3440×1440. Meter ranks and ability slots are static mock content. There is no dragging, saved panel placement, resource bar, portrait, buff, live meter calculation, ability activation, mobile layout or in-game key rebinding.

Play-mode checks confirmed advancing frames, four bindings, click/world selection synchronization, damage/heal/death/zero-cap/reset/re-enable/rebind display, hidden world bars across reset, center pointer access and panel pointer blocking. Log checks covered 50-entry eviction, retained scrollback, spell IDs and reset. Full-size Game views were visually inspected in normal and debug modes at 1280×720, 1920×1080, 2560×1440 and 3440×1440. Layout bounds and minimum frame text sizes passed at all four supported resolutions. The final Unity Console showed zero errors and warnings, and a Windows64 player build succeeded with zero errors. Player runtime interactions and physical V/alternate-key presses remain unverified; Unity's synthetic key command did not activate the pre-existing role hotkey either.

For T10, the Play-mode runner passed exclusive tab switching, independent scroll positions, debug replacement/restoration and reset checks. Current Game views were inspected at 1280×720 and 3440×1440; `CombatHudVerification.CheckLayout()` passed at all four supported resolutions. A Windows64 player build succeeded before the last scroll-follow correction. A rebuild through the running Editor timed out, and standalone tab input was not verified.

## Revisions

| Version | Date (YYYY-MM-DD) | Summary |
|---|---|---|
| v1.1 | 2026-10-22 | Added Combat/Decisions tabs with bounded independent histories, per-tab scroll/follow state, debug replacement restoration and T10 layout checks. |
| v1 | 2026-10-21 | Added reactive selectable party frames, responsive HUD, bounded live log, Editor debug swap, inert mocks and configurable world-bar visibility. |
