# Team member selection v1.1

**Status:** implemented

## Overview

Selection supports four friendly party members, one at a time. It stores the current selected unit and displays a visible ring beneath that unit. Selection does not issue movement or combat commands.

## Behavior

- Left-click a friendly unit's visible 3D body to select it.
- Press **1** for Tank, **2** for Healer, **3** for Melee DPS, or **4** for Ranged DPS.
- Left-click the arena, an obstacle, or a non-friendly object to clear the selection.

## Implementation

- `UnitSelectionController` lives on the arena camera and owns the current selection state, role hotkeys, and `SelectionChanged` event.
- Mouse picking uses `Camera.ScreenPointToRay` followed by a 3D `Physics.Raycast`. The ray checks the first collider hit, so scene geometry can occlude units.
- `SelectableUnit` identifies each party member and stores its `PartyRole` (`Tank`, `Healer`, `Dps`, or `RangedDps`). `Dps` remains the melee role to preserve existing serialized values. Only units on the `FriendlyUnit` layer can be selected by mouse.
- `SelectionRing` builds a procedural 3D ring mesh on the XZ plane. It follows the selected unit's world XZ position and aligns its height to the bottom of the unit collider.
- Input uses Unity's Input System. The selection feature has no movement or combat responsibilities.

## Source files

- `raid-commander-game/Assets/_Game/Scripts/Selection/UnitSelectionController.cs`
- `raid-commander-game/Assets/_Game/Scripts/Selection/SelectableUnit.cs`
- `raid-commander-game/Assets/_Game/Scripts/Selection/SelectionRing.cs`
- `raid-commander-game/Assets/_Game/Editor/UnitSelectionSceneSetup.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/RangedDpsVerification.cs`

## Current scope

Supports single selection across four party members, role hotkeys, clearing selection, and a ground ring. Group selection, drag-box selection and hostile selection are outside this version. T6's debug attack directly targets the boss independently of party selection. Play-mode verification passed for all four hotkeys, mouse selection, ring following, and movement continuing after changing selection.

## Revisions

| Version | Date (YYYY-MM-DD) | Summary |
|---|---|---|
| v1.1 | 2026-10-20 | Added the RangedDps role and hotkey 4, preserved existing role values, and verified four-unit input/ring integration. |
| v1 | 2026-10-18 | Added individual friendly-unit selection, role hotkeys, empty-click clearing, and an XZ selection ring. |
