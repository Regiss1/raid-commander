# Obsidian vault practices

- Hard rule: if an impactful clarification is needed, ask the requester (the person sending the prompt) and wait for their answer before implementing work that depends on it. Do not assume an answer. Impactful clarifications can change scope, behavior, architecture, acceptance criteria, or resolve meaningful conflicts or redundancy. Independent work may continue while waiting.
- Read the relevant notes here before changing project scope, architecture, or feature plans.
- Keep project notes consistent with the Unity repository when a task changes architecture, folder structure, MVP scope, or feature status.
- Document every game-system change in a dedicated note under `systems/`. Create `systems/<system-name>.md` when a system does not have a note yet, and update that note whenever the system changes.
- Maintain a `## Revisions` table in each system note. Add a version, date (`YYYY-MM-DD`), and concise change summary whenever its implementation changes; keep newest entries first.
- Structure system notes using this template:

  ```markdown
  # <System name> v<version>

  **Status:** <planned | in progress | implemented>

  ## Overview
  <Purpose and responsibilities>

  ## Behavior
  <Player-facing behavior and controls>

  ## Implementation
  <Important design and technical details>

  ## Source files
  - `<path>`

  ## Current scope
  <Supported behavior and known boundaries>

  ## Revisions
  | Version | Date (YYYY-MM-DD) | Summary |
  |---|---|---|
  | v<version> | YYYY-MM-DD | <Change summary> |
  ```

- Clearly label plans and long-term ideas. Mark a feature complete only after it exists in `raid-commander-game/`.
- Keep implementation in the Unity project; use this vault for planning, decisions, checklists, and design context.
- Update the smallest relevant note and avoid copying the same detailed specification into multiple files. Link to the canonical note where useful.
- Preserve the existing directory organization and save Markdown as UTF-8.
