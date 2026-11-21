# VSR Sales Engine — Agent Team

Each file here defines one agent (role, what it may touch, what it must hand back).
They are installed in `.claude/agents/`, which is where Claude Code loads agents from. Edit them there. This README holds the rules they share.

## Rules every agent follows
1. **Boundary:** work only inside `C:\Users\ADMIN\Codebase`. No external uploads (see `Codebase/AGENTS.md`).
   Notion and other outside services are NOT allowed. Status goes in `docs/DAILY_STATUS.md`.
2. **Branches:** one new branch per PR group, named `feature/PR-NN-Txxx-<name>` (ticket IDs in the PR title),
   flow: PR into `develop` -> "Release PR-NN" PR from `develop` into `main`.
   Never commit to `develop` or `main` directly.
3. **Auto-merge (standing owner rule, set 2026-11-22):** when a PR exists and all its checks have passed and
   GitHub reports it mergeable, merge it automatically with a merge commit (not squash), in the order listed in
   `TASKS.md`, then delete nothing and carry on. Never merge a PR whose checks are failing, pending or missing,
   and never force-push or bypass checks.
   - `feature -> develop` PRs: merge automatically as above.
   - `develop -> main` release PRs: also merge automatically as above, EXCEPT when the PR description lists a
     "Before merging" item that is still open (for example an unchecked deployment secret) - then stop and tell the owner.
   - This rule only covers merging. Deploys, pushes to anything other than the project's own `origin`, and
     external services stay under `Codebase/AGENTS.md`.
4. **No new external packages** without discussion and approval.
5. **File size:** backend files 300 lines max, React components 250 lines max, one responsibility per file.
6. **Backend shape:** Controller (thin) -> Service (rules) -> Repository (data). No data access in controllers.
7. **Done means checked:** run build and tests and report the real output before marking a task done in `TASKS.md`.
8. **Update `TASKS.md`** (counts too) and add the day's entry to `docs/DAILY_STATUS.md`.

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
