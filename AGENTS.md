# Project architecture rules

These rules apply to the Unity project in `raid-commander-game/`.

## Obsidian vault practices

- Read relevant notes in `obsidian-vault/` for project context before changing architecture, scope, or feature plans.
- Keep the vault aligned with the repository when a task changes project structure, architectural decisions, MVP scope, or feature status.
- Distinguish implemented behavior from planned direction. Update status/checklists only when the corresponding work is actually present in the Unity project.
- Treat `obsidian-vault/` as planning and project documentation; Unity implementation remains under `raid-commander-game/`.
- Preserve existing vault organization and write Markdown as UTF-8. Prefer updating the relevant note over duplicating the same specification across several files.

## World and coordinates

- The game is fully 3D. Use Unity 3D models, `Transform`, 3D colliders, 3D physics, and 3D navigation.
- Gameplay takes place on the XZ plane: X is left/right, Z is forward/back, and Y is elevation.
- Keep the arena floor on a horizontal plane. Ground destinations are world positions with Y resolved from the floor or NavMesh; do not treat Y as a planar movement axis.
- The camera is an elevated, angled, orthographic 3D camera. Screen-to-world selection and orders use 3D physics raycasts against the world.
- Use `Vector3`, `Physics` / `Physics.Raycast`, `Collider`, `NavMeshAgent`, and 3D character controllers when appropriate.
- Never introduce `Rigidbody2D`, `Collider2D`, `Physics2D`, 2D tilemaps, 2D renderers, or 2D movement systems.

## Controls and movement

- Use the Unity Input System package and input actions for player controls.
- Party control follows RTS conventions: select one or more units, then issue explicit world-space orders.
- A player order temporarily takes priority over a unit's autonomous role behavior. Keep the distinction between player orders and autonomous decisions explicit in component responsibilities.
- Ground movement and navigation targets must remain on the XZ plane, with elevation determined by the environment/navigation surface.
- Prefer NavMesh navigation for unit movement and pathfinding. Use 3D physics raycasts for picking and ground targeting.

## Code and content architecture

- Organize code by gameplay feature under `Assets/_Game/Scripts/`.
- Prefer small, composable `MonoBehaviour` components with clear ownership. Keep domain data and decision logic separate from scene presentation where practical.
- Avoid global managers, inheritance-heavy frameworks, and premature abstractions. Add shared code only when multiple features need it.
- Keep encounters, abilities, and class definitions data-driven where practical so future user-created content can be supported.
- Treat mod loading and executable scripting as future work. Do not add a scripting runtime or mod framework until there is a concrete requirement and security model.
- Keep third-party assets and packages clearly separated from first-party game code.
- Do not implement gameplay as part of project-setup or architecture-only tasks.

## MVP scope

The first playable target is a single arena with a tank, healer, DPS, and one boss. The player can select units and override autonomous movement; the units otherwise act according to their roles. Keep future systems compatible with additional classes, abilities, and moddable encounters without building those systems prematurely.
