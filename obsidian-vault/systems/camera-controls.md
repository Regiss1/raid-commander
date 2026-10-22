# Camera controls v1

**Status:** implemented

## Overview

The elevated orthographic camera presents the 3D arena and lets the player pan and zoom while issuing world-space orders.

## Behavior

- WASD or arrow keys pan along the ground plane. The mouse wheel zooms between configured orthographic bounds.
- Wheel scrolling over a visible combat HUD panel scrolls its content without zooming the arena. The open arena center remains available for camera zoom.

## Implementation

- `OrthographicArenaCamera` uses Input System actions for pan and wheel zoom, smooths both with unscaled time, and projects directional pan onto XZ.
- Before processing wheel zoom, it queries `UiPointerBlocker` against current uGUI geometry. HUD roots do not cover the center, and only visible graphics consume pointer input.

## Source files

- `raid-commander-game/Assets/_Game/Scripts/Camera/OrthographicArenaCamera.cs`
- `raid-commander-game/Assets/_Game/Scripts/UI/UiPointerBlocker.cs`
- `raid-commander-game/Assets/_Game/Scenes/RaidArena.unity`

## Current scope

Keyboard pan remains available while hovering the HUD. Zoom input is suppressed over visible HUD panels. Camera framing, bounds and zoom speeds remain configurable on the existing component; T9 adds no camera movement or viewport redesign.

## Revisions

| Version | Date (YYYY-MM-DD) | Summary |
|---|---|---|
| v1 | 2026-10-21 | Documented existing orthographic pan/zoom and added HUD wheel isolation. |
