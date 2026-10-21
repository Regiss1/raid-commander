# Raid Commander project vault

This vault holds project context, design notes, architecture planning, and development checklists for the Unity project in `../raid-commander-game/`.

## Notes

- `prompts/game-summary.md` is the broad game concept, MVP, roadmap, and long-term direction.
- `structure/folder.structure.md` describes the current Unity scaffold and planned areas for later growth.
- `checklists/development.checklist.md` tracks foundation and MVP progress.
- `systems/` contains dedicated notes describing implemented game systems and their current versions.
- [Team member movement](systems/team-member-movement.md) documents right-click orders, navigation, and command completion.
- [Autonomous party AI](systems/autonomous-party-ai.md) documents four roles, manual positioning priority, hazard/chase reactions, sight recovery and state/floor debugging.
- [Boss abilities and hazards](systems/boss-abilities-and-hazards.md) documents mobile boss targeting, Wall of Flames, clipped telegraphs, per-instance loadouts and the separate boss navigation bake.
- [Combat and event logging](systems/combat-and-logging.md) documents melee/ranged damage, the fourth cyan party unit, debug actions, health changes and encounter-scoped combat events.
- [Reactive health bars](systems/health-bars.md) documents party health displays and the boss HUD.
- `prompts/tasks/` contains task prompts and implementation planning.
- [Task implementation workflow](structure/task-workflow.md) defines task identity, revision planning, GitHub issue status, local review, and approval before commit, push, and completion.
- `AGENTS.md` records practices for maintaining this vault.

Treat the Unity repository as the source of truth for what is implemented. Keep future plans clearly distinguished from completed work.
