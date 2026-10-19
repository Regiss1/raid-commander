# Team member selection v1

**Status:** Implemented in the playable arena.

## Overview

The v1 selection system selects one friendly party member at a time. It stores the current selected unit and displays a visible ring beneath that unit. Selection does not issue movement or combat commands.

## Controls

- Left-click a friendly unit's visible 3D body to select it.
- Press **1** to select Tank, **2** to select Healer, or **3** to select DPS.
- Left-click the arena, an obstacle, or a non-friendly object to clear the selection.

## Implementation

- `UnitSelectionController` lives on the arena camera and owns the current selection state, role hotkeys, and `SelectionChanged` event.
- Mouse picking uses `Camera.ScreenPointToRay` followed by a 3D `Physics.Raycast`. The ray checks the first collider hit, so scene geometry can occlude units.
- `SelectableUnit` identifies each party member and stores its `PartyRole` (`Tank`, `Healer`, or `Dps`). Only units on the `FriendlyUnit` layer can be selected by mouse.
- `SelectionRing` builds a procedural 3D ring mesh on the XZ plane. It follows the selected unit's world XZ position and aligns its height to the bottom of the unit collider.
- Input uses Unity's Input System. The selection feature has no movement or combat responsibilities.

## Source files

- `raid-commander-game/Assets/_Game/Scripts/Selection/UnitSelectionController.cs`
- `raid-commander-game/Assets/_Game/Scripts/Selection/SelectableUnit.cs`
- `raid-commander-game/Assets/_Game/Scripts/Selection/SelectionRing.cs`
- `raid-commander-game/Assets/_Game/Editor/UnitSelectionSceneSetup.cs`

## Current scope

V1 supports single selection, role hotkeys, clearing selection, and a ground ring. Group selection, drag-box selection, and movement orders are outside this system version.

## Revisions

| Version | Date | Summary |
|---|---|---|
| v1 | 2026-10-18 | Added individual friendly-unit selection, role hotkeys, empty-click clearing, and an XZ selection ring. |
