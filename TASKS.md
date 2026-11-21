# VSR Sales Engine — Task Tracker

This is the **only** task tracker for the project (backend and frontend).
It replaces the old trackers: `EXECUTION_STATUS.md`, `progress-tracker.csv` (two copies) and
`docs/traceability/requirements-traceability-matrix.md`. They marked everything 100% done,
which was not true.

**Last updated:** 2026-11-21

## Count

| Status | Count |
|---|---|
| ✅ Done (checked) | 5 |
| 🟡 In progress | 6 |
| ⬜ To do | 6 |
| ⛔ Blocked | 2 |
| ❔ Claimed done earlier, not re-checked | 19 |
| ❌ Claimed done earlier, actually not done | 3 |
| **Total** | **41** |

Update this table whenever a task changes status.

**Status key:**
- ✅ Done and checked working
- 🟡 Started, not finished
- ⬜ Not started
- ❔ The old tracker said done; nobody has re-checked it
- ❌ The old tracker said done, but it does not work
- ⛔ Blocked by a rule or a missing decision

---

## Current work: make the lead hunter actually run

Goal: agents run every 1–2 hours on free plans (Render backend + Supabase database + Vercel/Netlify frontend).

| # | Task | Status | Notes |
|---|---|---|---|
| N01 | Real database storage (EF Core / Postgres) and background worker | 🟡 | Built and tested locally on branch `feature/live-persistence-and-autonomous-worker`. Not pushed. |
| N02 | Shift agents: run every N hours, live sources only (Hacker News, We Work Remotely, Remotive, Jobicy) | ✅ | Built and tested (136 tests pass) on branch `feature/hourly-shift-agents`. 3 shifts every 2 hours, windows in India time. Also fixed a bug where We Work Remotely was never fetched. |
| N03 | Secured endpoint `POST /api/agents/run-due` to wake and run due agents | ✅ | Built and tested. Needs the `Agents__RunKey` secret set on Render. |
| N04 | GitHub Actions "alarm clock" that calls N03 every 2 hours | 🟡 | File written (`.github/workflows/agent-run.yml`). Not run on GitHub yet: needs push plus secrets `API_BASE_URL` and `AGENT_RUN_KEY`. |
| N05 | Agent status endpoint and dashboard view (what each agent last found) | 🟡 | `GET /api/agents/status` done. Dashboard and Agents screen in the frontend still to do. |
| N06 | Connect Supabase (`ConnectionStrings__DefaultConnection`) | ⬜ | **Waiting on you:** Supabase connection string. |
| N07 | Push branch, open PR, deploy backend to Render, confirm `/health` | ⬜ | `/health` returned 404 on 2026-11-21. Never deployed. |
| N08 | Point the frontend `VITE_API_BASE_URL` at the real Render URL and redeploy | ⬜ | |
| N09 | Single honest task tracker (this file) | ✅ | Done 2026-11-21. |
| N10 | Design the architecture and whole workflow first (`docs/ARCHITECTURE.md`) | ✅ | Approved by you 2026-11-21, including the 7-step agent relay. |
| N11 | Refactor backend to repository pattern + MVC, standardise code | 🟡 | Dead entities removed. Layering already enforced by a test. Remaining: audit controllers for data access. |
| N12 | Split oversized files (limit 300 lines backend, 250 React) | 🟡 | Backend done: `LiveJobFeedService` 496→92, `ReliabilityService` 346→250, `OutreachDraftService` 321→205. `DiscoveryService.cs` is 305. Frontend `App.tsx` split in progress. |
| N13 | New UI design of the system, shown to you as a mockup | 🟡 | Mockup built: open `docs/ui-mockup/index.html` in a browser. Sample data only. Waiting for your review. |
| N14 | Agent team definitions (10 agents in `agents/`) | ✅ | Done 2026-11-21. Roster in `agents/README.md`. |
| N15 | Hourly dashboard agents: run every hour for a few minutes, fetch new leads and other data | ⬜ | Same work as N02–N05; N05 is the dashboard view. |
| N16 | Daily status log (`docs/DAILY_STATUS.md`), updated every day | ⬜ | Replaces the Notion idea (see N17). |
| N18 | For leads scoring over 50: auto-draft email and a LinkedIn message text, auto-send email only after the compliance gate passes | ⬜ | Email sending stays in test mode until a provider key and verified sending domain exist. |
| N19 | Auto-send LinkedIn connection requests and messages | ⛔ | LinkedIn bans automated connections and messages, and the app's own source policy (SRC-006) prohibits it. Replaced by a manual one-click handoff (profile link plus copy-ready message) inside N18. |
| N17 | Write status and architecture to a Notion folder "VSR Sales Engine" | ⛔ | `Codebase/AGENTS.md` forbids sending project data to outside services, with no approval exception. Needs you to change that rule yourself. |

---

## Earlier plan (T001–T070, PR-00 to PR-21)

These were all marked **DONE** before. Each one is re-marked honestly below.

| PR | Tickets | Scope | Status | Notes |
|---|---|---|---|---|
| PR-00 | T001–T005 | Baseline, source policies, legal boundaries, scope | ❔ | |
| PR-01 | T006–T008 | Solution skeletons, WebApi and Webapp structure | ❔ | |
| PR-02 | T009–T011 | Worker, Docker, CI baseline | ❔ | |
| PR-03 | T012–T014 | PostgreSQL entities, repositories, migrations | ❌ | Data was only held in memory and lost on every restart. Fixed by N01, not yet merged. |
| PR-04 | T015–T018 | Auth, JWT, guest mode gate, audit foundation | ❔ | |
| PR-05 | T019–T021 | Source contracts, discovery orchestration, run records | ❔ | |
| PR-06 | T022–T025 | Source adapters, manual handoff, attribution | ❌ | Most sources produce made-up placeholder leads. Only the 4 live feeds are real (see N02). |
| PR-07 | T026–T028 | Canonical records, deduplication, search profiles | ❔ | |
| PR-08 | T029–T031 | Qualification rules, scoring, restricted source policy | ❔ | |
| PR-09 | T032–T034 | Vendor readiness, compliance packs, filter state | ❔ | |
| PR-10 | T035–T037 | Dashboard, discovery runner, results table | ❔ | |
| PR-11 | T038–T040 | Detail view, pipeline stages, contact associations | ❔ | |
| PR-12 | T041–T043 | Audit history, activity stream, settings | ❔ | |
| PR-13 | T044–T046 | Contact provenance, role accounts, dedupe | ❔ | |
| PR-14 | T047–T049 | Grounded templates, draft preview, truthfulness guard | ❔ | |
| PR-15 | T050–T052 | Sandboxed send, provider adapter, pre-send suppression | ❔ | No real email provider set up (SendGrid key missing). |
| PR-16 | T053–T055 | Campaigns, sequences, compliance gate | ❔ | |
| PR-17 | T056–T058 | Scheduling, distributed lock, quiet hours | ❔ | "Run now" did not actually run discovery. Fixed by N01, not yet merged. Quiet hours wrongly use UTC (N02). |
| PR-18 | T059–T061 | Failure classification, backoff, dead letter | ❔ | |
| PR-19 | T062–T064 | Automated test depth, scoring, contract fixtures | ❔ | The old file claimed 121 backend tests passing. Not re-run. |
| PR-20 | T065–T067 | Browser E2E, accessibility, SSRF/XSS prevention | ❔ | |
| PR-21 | T068–T070 | Deployment pipelines, runbook, smoke drill, handover | ❌ | Backend was never deployed (`/health` returns 404). Covered by N06–N08. |

---

## Decisions made

- 2026-11-21: you approved `TASKS.md` and the architecture. Agents run **every 2 hours** (N02/N04).

## Waiting on you

1. Review the UI mockup (N13)
2. Supabase connection string (N06)
3. OK to push the branch and deploy (N07), not asked yet

## Original notes (verbatim, from you; turned into tasks N10–N17 above)

Branch rule from your notes: each new PR gets a new branch from `develop`, then PR into `develop`, then PR into `main`.
Prepare  desing  architecture of the application  first  and also the   whole work flow u have all the reosurces  and mcp  tool use thtat like  notion  add new fodler vsr sales enginer  and add there 

Always udpate the notion with ffinal  status  of the day 
3 redesing the code base  follow  repository pattern in backend lasong with  mvc same wiht  stndarise the code 

show me new ui of the  system 

use ai  driven  coding there are multiple agents use  all of them accriding to the need ![
](image.png)

I need  few agents  redisng  the dabord  for that they wil lrun  evey hour  for few minutes  to egt teh new  lead  and other thigns these wil lcusotm agents 


foll ow the reusl of prs hoe new prs  woll be created and  will be used   new dev new brna and merge order same  ne brnahc to debv to main 

 refactor the cod make it optimzie  one fiel should not have husge   or more  lines of code than reocmmended  