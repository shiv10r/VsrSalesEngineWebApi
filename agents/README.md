# VSR Sales Engine — Agent Team

Each file here defines one agent (role, what it may touch, what it must hand back).
To use them in Claude Code, copy or link them into `.claude/agents/`.

## Rules every agent follows
1. **Boundary:** work only inside `C:\Users\ADMIN\Codebase`. No external uploads (see `Codebase/AGENTS.md`).
   Notion and other outside services are NOT allowed. Status goes in `docs/DAILY_STATUS.md`.
2. **Branches:** one new branch per PR: `feature/<id>-<name>` -> PR into `develop` -> PR into `main`.
   Never commit to `develop` or `main` directly. Never push or merge without the owner's OK.
3. **No new external packages** without discussion and approval.
4. **File size:** backend files 300 lines max, React components 250 lines max, one responsibility per file.
5. **Backend shape:** Controller (thin) -> Service (rules) -> Repository (data). No data access in controllers.
6. **Done means checked:** run build and tests and report the real output before marking a task done in `TASKS.md`.
7. **Update `TASKS.md`** (counts too) and add the day's entry to `docs/DAILY_STATUS.md`.

## Roster
| Agent | Job |
|---|---|
| architecture | System design, workflow, folder structure |
| backend-dev | .NET API, repositories, services, hourly lead agents |
| frontend-dev | React app, splitting big files |
| ui-ux-designer | New UI and dashboard design |
| qa-engineer | Tests, real end-to-end checks |
| code-reviewer | Reviews every diff, enforces size and layering rules |
| security-reviewer | Secrets, auth, input handling, outreach compliance |
| devops | Git flow, CI, Render, Supabase, GitHub Actions schedule |
| doc-writer | README, architecture docs, daily status |
| task-manager | Owns `TASKS.md`, assigns work, tracks counts |
