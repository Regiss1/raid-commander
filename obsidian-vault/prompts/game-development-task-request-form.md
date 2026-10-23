# Game development task request form

Copy this form, fill in what you know, and leave uncertain answers as `Unsure`. Plain language is enough. This is an intake form for drafting a task, not an implementation request or a canonical numbered task file.

**Target task writer:** GPT-6 Astra, Low reasoning.

## Request

**Task type:** New feature / bug fix / refinement of an existing task / investigation / unsure  
**Proposed task ID:** T___ / undecided  
**Related task ID or system:**  
**Short title:**

## Objective

What should change, and why does it matter to the player or developer? State the desired outcome in one or two sentences.

> 

## Current behavior or problem

What happens now? For a bug, include how to reproduce it, what you expected, and what actually happened. If this is a new feature, write `New behavior`.

> 

## Desired behavior

Describe what the player sees or does, what the game does in response, and any important rules. Separate firm requirements from ideas you are open to changing.

**Must have:**
- 

**Flexible or optional:**
- 

## Example

Give one concrete situation. Mention the starting state, player action or game event, and expected result. Add an edge case if it matters.

> When ___, if the player ___, then ___.

## Inspirations

Link or attach sketches, screenshots, videos, games, or written references. For each, say which specific aspect to use and which aspects should *not* be copied. References are inspiration, not extra requirements.

| Reference or attachment | Use this aspect | Avoid or ignore |
|---|---|---|
| | | |

## Scope and boundaries

**Include in this task:**
- 

**Leave for later or explicitly exclude:**
- 

**Existing behavior that must keep working:**
- 

## Player controls and presentation

Fill this in if the task affects input, selection, movement, combat feedback, UI, audio, animation, camera, or accessibility. Describe the visible result and where it appears. Write `No change` if none apply.

> 

## Data, tuning, and content

List values or rules that must be configurable, content that must be authored, and any assets you will provide. Mark unknown values as `Unsure` rather than inventing a number.

> 

## Acceptance checks

List observable outcomes that would convince you the task is finished. Include a normal case and any failure or boundary case that matters. The task writer can refine these into testable criteria.

1. 
2. 
3. 

## Dependencies and constraints

Name related systems or tasks, required order of work, target platform or resolution, performance limits, and any existing project decisions this must respect. Write `Unsure` if you want the task writer to investigate.

> 

## Open decisions

What choices should the task writer ask you to make? If you have a preference, state it and explain why. Mark any decision that would change behavior or scope.

- 

## Handoff for the task-writing agent

Use this filled form to **draft a bounded game development task for later implementation**. Read `AGENTS.md`, relevant vault system notes, `obsidian-vault/structure/task-workflow.md`, and the actual Unity implementation before treating a described feature as absent. Preserve the requester's firm requirements and exclusions; distinguish inspiration and suggestions from requirements. Identify overlap with existing tasks and any impactful decisions, ask the requester focused multiple-choice questions, and wait for answers before settling dependent scope. Do not fill gaps with invented mechanics, tuning, assets, or acceptance claims.

Produce a task draft with an objective, current baseline, included behavior, exclusions, dependencies, concrete acceptance criteria, and a verification plan. Use `obsidian-vault/prompts/tasks/T<number>-brief-title.md` only after the requester confirms an unused task ID and the draft is sufficiently bounded; follow the repository's task workflow for validation and issue tracking. Do not change Unity code or scenes, claim a feature is implemented, or start implementation from this form alone.
