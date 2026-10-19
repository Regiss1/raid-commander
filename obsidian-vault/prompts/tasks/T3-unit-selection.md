TASK: Implement Warcraft III-style individual unit selection in our fully 3D, top-down Unity game.

Requirements:
1. Use Camera.ScreenPointToRay and Unity 3D Physics.Raycast to identify clicked units.
2. Left-click selects one friendly unit.
3. Keyboard shortcuts 1, 2, and 3 select Tank, Healer, and DPS.
4. Clicking empty ground clears selection.
5. Show a visible selection ring on the ground underneath the unit, following its world position.
6. Keep the selection ring aligned to the XZ ground plane.
7. Use a central selection service or controller that does not contain movement or combat logic.
8. Unit selection must be independent of movement and AI logic. Selecting a unit should only update the selection state and visual indicator. Do not implement or reference autonomous AI behavior in this task.

Use the Unity Input System. Do not use 2D colliders or screen-space approximations for unit detection.

Acceptance criteria:
- Selection works correctly from the angled top-down camera.
- Units can be selected by clicking their visible 3D bodies.
- Selection ring stays at the unit's feet.
- Keyboard and mouse selection are consistent.