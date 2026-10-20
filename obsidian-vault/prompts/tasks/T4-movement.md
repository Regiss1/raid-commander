<!-- task-tracking:start -->
**Task:** T4
**GitHub issue:** [#5](https://github.com/Regiss1/raid-commander/issues/5)
**Tracking status:** done (historical completion explicitly confirmed by requester)
**Implementation commit:** [5a247b6](https://github.com/Regiss1/raid-commander/commit/5a247b6c014bdc8bdba76d2b2452e341f8642414)
<!-- task-tracking:end -->

TASK: Implement right-click movement for selected units in the 3D top-down arena.

Requirements:
1. Use raycasts from the camera to determine the clicked 3D world-space ground position.
2. Use Unity AI Navigation with NavMeshSurface and NavMeshAgent.
3. Bake a NavMesh on the arena's walkable geometry.
4. Right-click orders the selected unit to move to the destination.
5. Units must navigate around 3D obstacles.
6. Show a temporary destination marker on the ground.
7. Reject unreachable destinations gracefully.
8. Separate movement command execution from the source of the command (player or AI).
9. Manual commands override autonomous movement temporarily and have a defined completion state.

Movement uses 3D Vector3 coordinates, with navigation occurring primarily on the XZ plane. Ground elevation may vary in future levels.

Do not use Transform.Translate as a substitute for pathfinding, Physics2D, or 2D grid movement.

Acceptance criteria:
- Each party member can move independently.
- Units avoid obstacles.
- Camera angle does not affect movement accuracy.
- New commands can interrupt movement.
- Manual movement completes cleanly.