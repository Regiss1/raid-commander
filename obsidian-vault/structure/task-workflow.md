# Task implementation workflow v1.5

**Status:** implemented; the workflows, scripts, and updated commit convention are published on `main`, and both Actions workflows are active. Historical tasks T1, T2, T3, T4, T5, and T5.1 are backfilled as completed GitHub Issues with requester confirmation.

## Task identity and specification

Task specifications live in `obsidian-vault/prompts/tasks/`. Every task filename follows `T<task-number>-brief-title-of-task.md`. Whole and dotted numbers are supported: `T5`, `T5.1`, and `T5.10` are distinct. Leading zeros do not make a new identifier, so `T05.1` conflicts with `T5.1`. Duplicate numbers and malformed task filenames fail validation.

GitHub Issues track execution state; the task file remains the canonical specification. The GitHub issue number is separate from the task identifier. Each tracked issue contains exactly one marker, such as `<!-- raid-commander-task:T6 -->`, and a `Task file:` line pointing to its specification. Search both open and closed issues before creating one or implementing a task. Multiple issues for the same task require requester resolution.

An old task without an issue is **untracked**, not automatically unimplemented. Read the current implementation, vault system notes, and development checklist before proposing work. Do not repeat historical work or create retrospective issues without authorization.

## Historical backfill

The requester explicitly authorized importing the committed tasks T1 through T5.1 and confirmed all six as already approved and completed. Each issue has the canonical task marker and file mapping, original published commit evidence, and a clear historical verification record. Task files contain the matching issue links; their original requirements are preserved.

| Task | GitHub issue | State | Original implementation commit |
|---|---|---|---|
| T1 | [#2](https://github.com/Regiss1/raid-commander/issues/2) | closed / completed / `task:done` | [9fd1e72](https://github.com/Regiss1/raid-commander/commit/9fd1e726136b4ae32e0920f11ae00c6772c1c380) |
| T2 | [#3](https://github.com/Regiss1/raid-commander/issues/3) | closed / completed / `task:done` | [d20fc1c](https://github.com/Regiss1/raid-commander/commit/d20fc1c54e23a4bb3e950e10f1032880316d67a0) |
| T3 | [#4](https://github.com/Regiss1/raid-commander/issues/4) | closed / completed / `task:done` | [5ff2c60](https://github.com/Regiss1/raid-commander/commit/5ff2c60961be96a098d3e304ad6f6b4beea9d2a1) |
| T4 | [#5](https://github.com/Regiss1/raid-commander/issues/5) | closed / completed / `task:done` | [5a247b6](https://github.com/Regiss1/raid-commander/commit/5a247b6c014bdc8bdba76d2b2452e341f8642414) |
| T5 | [#6](https://github.com/Regiss1/raid-commander/issues/6) | closed / completed / `task:done` | [91e3c9f](https://github.com/Regiss1/raid-commander/commit/91e3c9f0d2b5e3c3667cf1b006b16051e5069e25) |
| T5.1 | [#7](https://github.com/Regiss1/raid-commander/issues/7) | closed / completed / `task:done` | [8c42360](https://github.com/Regiss1/raid-commander/commit/8c42360ae8990104eca4a8db63cf7b87a8acd2b2) |

T5.1 is a native GitHub sub-issue and recorded scope revision of T5. Both remain completed; no new blocking dependency was introduced. T6 and uncommitted local scene changes were excluded.

The evidence snapshot is [2f499c7](https://github.com/Regiss1/raid-commander/commit/2f499c742058ff9404750983c4cadb4b27400ba5) on the published feature branch. This import records prior implementation and verification, not a new Unity test run. Original commits predate the current commit-subject convention and were not rewritten. Historical issue closure used the explicit backfill approval; future task completion continues to use the normal review, commit, push, and validation rules.

## Lifecycle and review

| Issue label | Issue state | Meaning |
|---|---|---|
| `task:planned` | Open | Defined task, awaiting planning/implementation. |
| `task:in-progress` | Open | An agent is implementing the agreed task. |
| `task:awaiting-review` | Open | Implementation and verification are ready; changes remain uncommitted for requester review. |
| `task:done` | Closed, completed | The requester approved the task, its changes were committed and pushed, and completion checks passed. |

Before work, inspect the issue. Refuse repeat implementation of closed or done tasks. An in-progress task may only resume when the requester is explicitly continuing that work; an awaiting-review task should remain available for review or requested revisions. A closed task can only be reopened by a separate explicit requester decision; the helper never reopens it automatically. A follow-up with a different objective needs a new task number.

For each accepted task, read applicable `AGENTS.md` files, README files, vault notes, and implementation. Check its task filename and number, then run the multiple-choice Plan and Clarification flow. Decline vague or open-ended implementation requests. After required questions are resolved, mark the issue in progress and implement the bounded plan.

After verification and vault updates, leave the task changes uncommitted, mark the issue awaiting review, and hand off the task ID, issue URL, modified files, and verification results. Do not commit, push, or close the issue at this stage.

The requester's task-specific approval, preferably `OK T6`, authorizes committing the reviewed changes, pushing to the agreed branch, and closing the corresponding issue. A plain `OK` is sufficient only when the conversation identifies exactly one task and review checkpoint. Never infer approval from a GitHub label, silence, or an unrelated message. If the reviewed changes materially change, obtain approval for the new checkpoint.

Before committing, recheck the diff and stage only the reviewed task files or hunks, preserving unrelated user work. If the branch contains other unpushed commits outside the approved scope, resolve what may be published before pushing. Use a normal push; do not force-push or merge as part of task approval.

## Revise operation

Use the existing `implementing-game-development-tasks` skill with a request such as `revise T5` or `revise T5.1`. This selects a planning-only operation: inspect the original task and implementation, classify the discovery, ask multiple-choice questions, and record the agreed correction or revision. It stops before game implementation, commit/push, or issue completion. A later explicit `implement T5.2` request starts normal development.

| Discovery | Planning outcome |
|---|---|
| An agreed acceptance criterion is unmet while the original task is open | A correction plan under the same task and issue. |
| Scope or acceptance criteria change | A new dotted revision task and its own planned issue. |
| A separate objective is requested | A new whole-number task. |
| Work is discovered after the original task was completed | A linked follow-up task; the completed parent stays closed. |

New revisions use flat siblings: `T5.1`, `T5.2`, and so on. Revising `T5.1` normally creates `T5.2`, records parent `T5`, and records `Revises: T5.1`. Inspect both task files and all open/closed tracked issues before proposing the next number, confirm it during clarification, and recheck uniqueness before saving. Do not reuse old numbers; `T5.10` is followed by `T5.11`. Preserve existing task files and legacy numbering.

Each revision records its own issue, parent task/issue, the task it revises, reason, bounded objective, scope changes, preserved requirements, acceptance criteria, exclusions, and dependencies/review boundaries. Preserve the original task's agreed scope; append relationships instead of rewriting its acceptance baseline. If a historical parent is untracked, ask whether to create its missing tracking or explicitly record it as untracked; do not automatically backfill it.

During review, clarify whether to approve the original scope first, keep the parent open with completion blocked by the revision, propose superseding an obsolete objective, or defer the revision. A parent relationship alone does not establish a blocking dependency. Record the chosen relationship without changing the parent's lifecycle status during revision planning. Proposed supersession is not completion: retiring the parent requires a separate explicit instruction and must not use the helper's `done` transition.

Use GitHub parent/sub-issue and blocking relationships where applicable and verify them after writing. Keep the revision issue `task:planned`. The current helper supports dotted IDs and each issue's existing lifecycle; it does not automate revision allocation, cross-issue dependency enforcement, or supersession. The skill checks these relationships before completion.

`OK T5` does not approve `T5.2`. If revision implementation begins while parent changes remain uncommitted, establish the original review checkpoint and separate the task file/hunk scopes. Do not publish unapproved parent changes through a revision's approval. If a combined review is necessary, obtain explicit approval naming both tasks and preserve separate task commits and issue accounting.

## Commit message convention

The first line of each task commit follows this convention:

```text
<type>(<branch-suffix>) => Brief description of the changes T<TaskId>  #<IssueId>
```

| Prefix | Purpose |
|---|---|
| `feat` | New features. |
| `refact` | Refactoring work or refactoring sprints. |
| `fix` | Bug fixes. |

Use the final slash-separated segment of the branch receiving the approved push as `branch-suffix`. For `feature/sprint-1/base-game`, use `base-game`. Include the actual task number with a single `T` prefix and the linked GitHub issue number; dotted task IDs remain valid. Keep one space before the task identifier and two spaces before `#<IssueId>`. The brief description must be nonempty. Choose the prefix that matches the task's primary purpose; resolve an impactful ambiguity with the requester.

Examples:

```text
feat(base-game) => Add ranged DPS attacks T6  #42
refact(base-game) => Simplify combat event dispatch T7  #43
fix(base-game) => Correct health reset notifications T8  #44
```

An optional commit body can provide details. The old `Task:` and `Refs` trailers are no longer required by the updated helper. Do not use automatic issue-closing keywords to bypass completion checks. The issue closes only after the helper verifies the pushed commit subject, branch ancestry, and task mapping. If push or completion fails, report the exact stopping point. Retry completion against the existing commit; do not create another commit to repair labels or issue closure.

## Local helper

Requires Node.js 24 and authenticated GitHub CLI with access to `Regiss1/raid-commander`. These commands run from the repository root. Replace example task, issue, branch, and commit values with the actual ones.

```powershell
node .github/scripts/task-tracking.cjs validate
node .github/scripts/task-tracking.cjs inspect --repo Regiss1/raid-commander --task T6
```

If no issue exists and the bounded task is confirmed to be new work, create exactly one issue. This does not backfill the rest of the vault:

```powershell
node .github/scripts/task-tracking.cjs create --repo Regiss1/raid-commander --task T6
node .github/scripts/task-tracking.cjs transition --repo Regiss1/raid-commander --task T6 --status in-progress
node .github/scripts/task-tracking.cjs transition --repo Regiss1/raid-commander --task T6 --status awaiting-review
```

After explicit approval, commit and push the reviewed changes first. Only then run completion, using the actual full 40-character SHA and pushed branch:

```powershell
$approvedTaskCommit = git rev-parse HEAD
node .github/scripts/task-tracking.cjs transition --repo Regiss1/raid-commander --task T6 --status done --approval 'OK T6' --commit $approvedTaskCommit --branch 'feature/ranged'
```

The helper verifies the issue is awaiting review, the approval names the task, the commit subject follows the convention and matches the branch suffix/task/issue, the commit is reachable from the pushed branch, and the pushed task folder has valid unique filenames with the expected issue mapping. It preserves unrelated labels. Completion closes the issue as completed and labels it done. Failures can be retried with the same approved commit, including a label failure after successful closure. The helper does not inspect chat or local review history; the agent must establish approval and reviewed file scope before invoking it. Passing an approval string without requester approval is not permitted.

## GitHub Actions

- `.github/workflows/task-validation.yml` runs the helper tests and validates task filenames and duplicate numbers on pushes and pull requests. It does not run Unity tests.
- `.github/workflows/task-lifecycle.yml` exposes explicit status transitions through `workflow_dispatch`. It uses the same helper, grants only contents-read and issues-write access, and runs its code from the default branch. It neither commits code nor interprets issue comments as approval. It uses `GITHUB_TOKEN`; no personal token secret is required.
- Lifecycle inputs are passed as environment variables, not inserted into executable script text. Submitted task branches are inspected through the GitHub API rather than executed by the write-enabled workflow.
- Uncommitted files are invisible to Actions. Use the local helper for new task files and local review checkpoints; remote completion checks inspect the pushed commit.

The workflows and helper were published to `main` through the workflow-only branch `chore/task-tracking-workflows` and merge commit `cf5aeac`. The lifecycle workflow can now be dispatched with repository write access. See [GitHub's manual workflow documentation](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow).

The updated commit subject validator was published to `main` through `fix/task-commit-subjects` and merge commit `5928502`. Both the local helper and the published lifecycle workflow now support the `feat`/`refact`/`fix` convention without requiring legacy trailers.

For example, after approval and a successful push:

```powershell
gh workflow run task-lifecycle.yml --repo Regiss1/raid-commander --ref main -f task=T6 -f status=done -f 'approval=OK T6' -f commit=$approvedTaskCommit -f branch=feature/ranged
```

A dispatch response means queued, not completed. Watch the specific run and inspect the resulting issue before reporting success. The local helper is also a supported way to complete an approved task; do not run both paths concurrently.

## Verification

Run `node --test .github/scripts/task-tracking.test.cjs` and `node .github/scripts/task-tracking.cjs validate`. The automated suite uses a fake GitHub API and does not create or modify real issues. All 33 tests pass locally and in [the updated validation run on main](https://github.com/Regiss1/raid-commander/actions/runs/39486112693), and all seven current task filenames pass local validation. On branches without a task folder, the validation workflow skips the filename scan; local task creation still requires an existing task file. A live lifecycle transition has not been exercised and requires a real tracked task.

## Revisions

| Version | Date (YYYY-MM-DD) | Summary |
|---|---|---|
| v1.5 | 2026-10-20 | Backfilled six committed tasks as requester-approved completed issues, linked original published evidence, and related T5.1 to T5 without changing requirements or gameplay. |
| v1.4 | 2026-10-20 | Added the skill's planning-only revise operation, scope classification, flat dotted revision tasks, parent relationships, and separate review/approval boundaries. |
| v1.3 | 2026-10-20 | Published the two updated automation scripts to main, verified all 33 tests in Actions, and enabled remote completion using the new commit convention. |
| v1.2 | 2026-10-20 | Added task commit subjects with feat/refact/fix prefixes, final branch segment, task ID and issue ID; updated local completion checks to replace mandatory legacy trailers. |
| v1.1 | 2026-10-20 | Published only workflows/scripts to main through an isolated branch, verified both workflows active and validation successful, and handled the scaffold's missing task folder. |
| v1.0 | 2026-10-20 | Added unique task validation, GitHub issue lifecycle, uncommitted review, explicit approval before commit/push/closure, and local/Actions helpers; no historical issue backfill. |
