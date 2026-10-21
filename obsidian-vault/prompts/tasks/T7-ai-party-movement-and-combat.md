TASK: Implement autonomous party AI for the fully 3D, top-down raid prototype.

Party roles:

- Tank: navigates toward the boss, enters melee range, and attacks.
- DPS: navigates to attack range, maintains suitable distance, and attacks.
- Healer: positions away from the boss, monitors party health, and heals injured allies.
- If any of them can’t get line of sight of their target they will path around the obstacle to get line of sight

Requirements:

1. Implement lightweight finite state machines or an equivalently simple decision system.
2. AI movement uses the existing 3D NavMesh navigation and command execution systems.
3. Units should orient toward their combat targets when appropriate.
4. AI must respect attack ranges and distances in the 3D world.
5. Manual player movement commands temporarily override autonomous positioning.
6. Autonomous decisions resume after manual movement completes.
7. Selecting a different unit must not interrupt the behavior of other units.
8. Give the boss simple periodic attacks against the Tank, to exercise the Healer AI.
9. Avoid expensive target searches every frame.
10. Provide visual debugging for the current AI state of each unit.
11. There should be a togglable debug option in the debug panel to make it so the boss health can’t get to 0, clamping to 1.

Do not implement complex boss mechanics, full threat tables, advanced formations, or manual spellcasting yet.

Acceptance criteria:

- All four party members independently fulfill their combat roles.
- Units navigate around obstacles in the arena.
- Player movement overrides work.
- Other characters continue behaving autonomously.
- A complete simple encounter can run without direct player input.

## Tracking

- GitHub issue: https://github.com/Regiss1/raid-commander/issues/10
- Status: implementation verified and requester approved on 2026-10-20. Publication and completion are tracked in the linked GitHub issue. Required clarification resolved on 2026-10-20.

## Initial implementation plan

- Add lightweight per-unit role decisions using the existing `UnitMovement` executor and `CombatSystem` resolver. Selection remains independent.
- Use assigned encounter references, configurable action intervals and throttled decisions rather than per-frame target searches.
- Find reachable XZ combat positions with clear obstacle sight; retain NavMesh-resolved elevation, unit passability and manual movement priority.
- Wire all four party roles, periodic boss attacks, AI state debugging and the boss one-health debug toggle into the saved arena and repeatable setup utilities.
- Verify unattended combat, obstacle routing, manual movement and resume, independent units, death/reset, health/log/tracer integration, compilation and actual Game-view readability.
- Exclude complex boss mechanics, threat tables, advanced formations and manual spellcasting.

## Requester decisions

- Manual movement pauses autonomous positioning only. Attacks/heals continue when range and line of sight permit.
- Healer prioritizes an injured living Tank, then other injured living allies, including self.
- Boss remains stationary, attacks the living Tank periodically within melee range and stops when the Tank dies.
- Completion means an unattended boss kill followed by idle party AI. The existing debug health/log reset remains available; victory/defeat screens and a new restart flow are excluded.

## Refined implementation and verification boundary

- Tank and Melee DPS approach the boss within melee range. Ranged DPS maintains a configurable preferred distance within ranged range. Healer seeks safe spacing from the boss while keeping its healing target within range and sight.
- After Tank priority, choose other injured allies by lowest health percentage, with stable encounter ordering for ties. Never revive dead allies. Self-healing uses the normal healing pipeline.
- AI checks obstacle sight for its chosen actions, including melee positioning; existing trusted debug melee behavior is preserved.
- Manual command completion, cancellation, failure or interruption must release positioning ownership predictably. New valid manual orders retain priority; selection changes do not affect role decisions.
- The boss damage floor is off by default, clamps effective damage at one remaining health when enabled, and emits no death event while protected. Disabling it restores normal lethal damage; it does not revive a dead boss.
- Action intervals, decision frequency and preferred distances are tunable component values. Cached encounter references avoid global target searches every frame.
- Add focused Play-mode checks for all four roles, boss cadence/range/death, self-healing, obstacle recovery, manual combat and resume, independent units, dead/unreachable targets, reset, damage-floor events and unattended encounter completion. Recheck existing combat, movement and reactive UI integrations and inspect the actual Game view.

## Implemented result

- Four per-unit `PartyRoleBrain` components reuse `UnitMovement` and `CombatSystem`; `BossBasicAttack` owns stationary periodic Tank attacks. Bounded `CombatPositionSearch` selects reachable positions with clear XZ sight. Action/search intervals and preferred distances are configurable.
- Implemented Tank-first healing, lowest-health-percentage fallback including self, continuing combat during manual movement, autonomous resume, independent selection, no revival, and idle party AI after boss death.
- The scrollable F8 panel displays all AI states/targets and the runtime boss health-floor toggle. Effective damage/death events respect that floor, including the debug kill action.
- `PartyAiSceneSetup` updates the current arena in place; full combat and arena-generation paths share its wiring. Repeated setup preserves component identity and custom tuning. Saved scene is configured without rebaking or replacing geometry.
- Dedicated documentation: [Autonomous party AI v1.0](../../systems/autonomous-party-ai.md). Updated movement/combat notes, project structure, checklist and README.

## Verification (2026-10-20)

- Unity 6000.6.6f1 compilation finished without errors. The real player-loop frame wait succeeded (`3863 -> 3882`), independently confirming advancing frames.
- Final Console ground-truth check: not compiling, compilation failure false, zero current errors or warnings.
- **PASS:** focused AI Play-mode suite: unattended boss kill with all roles; boss timing/range/dead Tank; Tank-first/ally/self healing; no revival; manual attacks without replacing the manual order; autonomous resume after completion/cancellation; independent selection/units; saved-column sight recovery for melee/ranged/healer; ranged retreat; unreachable target handling; boss floor/effective events; death and reset recovery.
- **PASS:** existing movement suite, including synthetic Input System input, markers, concurrent commands, navigation obstacles, invalid/partial targets, command terminal states, stall failure, manual ownership release, pass-through/shared destinations and selection colliders. Its fixture now suspends AI on Play entry and compares command IDs against the pre-input value, allowing autonomous startup commands.
- **PASS with original health-bar fixture:** combined combat, reactive-health, ranged and line-of-sight suites. The preserved pre-existing five-pixel bars fail the old eight-to-twelve-pixel readability assertion (`5.56189` at zoom 8). The combined suite passed with ten-pixel bars applied only inside its temporary Play session. The saved five-pixel values and existing assertion remain unchanged.
- **PASS:** actual Game-view visual inspection at camera sizes 8, 20 and 30, including per-role state rows, boss one-health behavior, visible controls and scrolling. Captures are in ignored `raid-commander-game/Logs/t7-ui-*.png`.
- **PASS:** AI setup applied repeatedly with four stable party components and one boss component; no duplicates. Final scene is saved, all four brains remain enabled, Editor is out of Play mode, runtime boss protection is off, and the original `runInBackground = false` is restored. Temporary capture assets were removed from `Assets/`.
- Task helper validates all ten current task filenames/numbers. Source/Markdown diff checks pass. New blank `m_Name` serializer fields use Unity's normal trailing-space formatting.
- Evidence: ignored `Logs/t7-ai-verification.txt` and `Logs/t7-console-history.json` under the Unity project. Two rejected capture-path commands were tooling validation errors, not gameplay errors; capture paths were corrected and diagnostics archived.

## Review scope and preserved requester work

- T7 scope: three new runtime Units scripts, AI Editor setup, focused verification runner and their `.meta` files; combat resolver/reset/sight/self-heal changes; debug producer/panel; setup wiring; existing verification isolation; five new scene components plus the debug-boss reference; task/system/project documentation.
- Preserve unrelated `.gitignore` changes and the requester's new T8 task. These are excluded from T7.
- The Editor initially held unsaved four-party health-bar heights of five pixels and Tank stats with melee range 2 / ranged range 60. Those values were preserved when the scene/assets were saved. The four `screenHeight: 10 -> 5` scene hunks and `TankCombatStats.asset` changes remain requester-owned, outside T7's eventual commit scope. A pre-AI scene copy is in ignored `Logs/t7-before-ai.unity`.
- No commit, push, merge or issue closure is authorized by this implementation handoff. The issue stays open awaiting task-specific review.

## Requester approval (2026-10-20)

- The requester explicitly approved both reviewed tasks: "T7 and T7.1 OK". This authorizes separate task commits and a normal push to `feature/sprint-1/base-game`; complete T7.1 before T7 to resolve the recorded blocker.
- The review handoff and revision-planning entries above are historical checkpoints. GitHub tracks the current publication/completion state.
- Preserve unrelated local work, including balance retuning after review, five-pixel party bars, Tank range edits, `.gitignore`, project settings and T8. Publish the reviewed party attack-speed defaults of 1.
