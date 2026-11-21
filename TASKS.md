# VSR Sales Engine — Task Tracker

The only task tracker for the project (backend and frontend). It replaced four old trackers that
marked everything 100% done, which was not true. The daily log is at the bottom.

**Last updated:** 2026-11-22

## Count

| Status | Count |
|---|---|
| ✅ Done and checked | 17 |
| 🟡 In progress | 3 |
| ⬜ Not started | 6 |
| ⛔ Blocked | 2 |
| ❔ Old tasks marked done, not re-checked | 20 |
| ❌ Old tasks marked done, actually not done | 2 |
| **Total** | **50** |

Update this table whenever a task changes status. Key: ✅ checked working · 🟡 started · ⬜ not started ·
⛔ blocked by a rule or decision · ❔ old tracker said done, unverified · ❌ old tracker said done, but it is not.

---

## Current work

| # | Task | Status | Notes |
|---|---|---|---|
| N01 | Real database storage (EF Core / Postgres) and background worker | ✅ | Merged (PR-22). Tested with the in-memory database only; Postgres/Supabase not connected yet (N06). |
| N02 | Shift agents every 2 hours, live sources only, India-time windows | ✅ | Merged (PR-25). Also fixed We Work Remotely never being fetched. |
| N03 | `POST /api/agents/run-due` secured by `X-Agent-Key` | ✅ | Merged (PR-25). Needs `Agents__RunKey` set on Render. |
| N04 | GitHub Actions timer calls run-due every 2 hours | 🟡 | `.github/workflows/agent-run.yml` merged but never run: needs secrets `API_BASE_URL` and `AGENT_RUN_KEY`, and a deployed backend. |
| N05 | Agents screen, live feed and Dashboard panel | ✅ | Merged (PR-27 backend, PR-28 frontend). 8 agents, real events. |
| N06 | Connect Supabase (`ConnectionStrings__DefaultConnection`) | ⬜ | **Waiting on you:** the Supabase connection string, set on Render. |
| N07 | Deploy backend to Render and confirm `/health` | ⬜ | `/health` returned 404 on 2026-11-21: never deployed. See `docs/DEPLOYMENT.md`. |
| N08 | Point the frontend `VITE_API_BASE_URL` at the Render URL, then check the Vercel CSP | ⬜ | The CSP allows only `https://vsr-sales-engine-webapi.onrender.com`. |
| N09 | Single honest task tracker (this file) | ✅ | |
| N10 | Architecture and workflow design (`docs/ARCHITECTURE.md`) | ✅ | Rewritten 2026-11-22 to match what is built. |
| N11 | Repository pattern + MVC across the backend | 🟡 | Layering is enforced by a test. Remaining: audit controllers for direct data access. |
| N12 | Split oversized files (300 lines backend, 250 React) | ✅ | `DiscoveryService.cs` is still 305 lines. |
| N13 | New UI: Agents workspace, heat map, sign-in screen | ✅ | PR-28, PR-33, PR-34. Not yet click-tested in a browser by automation. |
| N14 | Development agent team installed in `.claude/agents` (both repos) | ✅ | PR-30. Rules in `agents/README.md`. |
| N15 | Hourly dashboard agents (fetch new leads, show on the dashboard) | ✅ | Same as N02 + N05. |
| N16 | Daily status log | ✅ | Now the section at the bottom of this file. |
| N17 | Publish the architecture to Notion | ⛔ | `Codebase/AGENTS.md` forbids outside services with no approval exception. Import `docs/ARCHITECTURE.md` into Notion yourself (Import → Markdown). |
| N18 | Auto-draft for leads scoring over 50; auto-send email after compliance passes | 🟡 | Drafter and Compliance exist and are unit-tested. Nothing is sent (test mode). Live runs rarely reach 50 (see N27). |
| N19 | Auto-send LinkedIn connections and messages | ⛔ | LinkedIn prohibits it and so does the app's own source policy (SRC-006). Manual handoff only. |
| N20 | Website security: headers, rate limits, guest guard, secret-key check, CSP | ✅ | PR-26 (backend + frontend). |
| N21 | Filter irrelevant leads using the role filter | ✅ | PR-27: Priya drops non-software roles with a stated reason. |
| N22 | Sign-in screen redesign, no pre-filled credentials | ✅ | PR-34. |
| N23 | Lead heat map on the Dashboard | ✅ | PR-31 backend, PR-33 frontend. Real data only. |
| N24 | Hash passwords; seed demo users only in Development | ✅ | PR-32. Set `Seed__AdminEmail` / `Seed__AdminPassword` on Render or nobody can sign in. |
| N25 | Consolidate markdown docs | ✅ | This change (PR-35). |
| N26 | Database migration for `AgentRuns` / `AgentEvents` | ⬜ | Needed only if a database already exists; a fresh Supabase database is fine. |
| N27 | Tune lead scoring so strong leads reach the draft threshold | ⬜ | Live feeds rarely say ".NET"; top live score so far is 20. Needs your keywords and threshold. |
| N28 | Upgrade `System.Security.Cryptography.Xml 9.0.0` (11 known advisories) | ⬜ | Installing a package: needs your approval. |

## Earlier plan (T001–T070, PR-00 to PR-21)

All of these were marked DONE before. Re-marked honestly.

| PR | Tickets | Scope | Status | Notes |
|---|---|---|---|---|
| PR-00 | T001–T005 | Baseline, source policies, legal boundaries | ❔ | |
| PR-01 | T006–T008 | Solution skeletons | ❔ | |
| PR-02 | T009–T011 | Worker, Docker, CI baseline | ❔ | |
| PR-03 | T012–T014 | PostgreSQL entities, repositories | ❔ | Was in-memory only; fixed by N01, not yet tried on real Postgres. |
| PR-04 | T015–T018 | Auth, JWT, guest gate, audit | ❔ | Passwords were plain text; fixed by N24. |
| PR-05 | T019–T021 | Source contracts, discovery orchestration | ❔ | |
| PR-06 | T022–T025 | Source adapters, handoff, attribution | ❌ | Only the 4 live feeds are real. "Company careers", "Public procurement" and others make placeholder leads. |
| PR-07 | T026–T028 | Canonical records, dedupe, search profiles | ❔ | |
| PR-08 | T029–T031 | Qualification, scoring, restricted sources | ❔ | |
| PR-09 | T032–T034 | Vendor readiness, compliance packs | ❔ | |
| PR-10 | T035–T037 | Dashboard, discovery runner | ❔ | |
| PR-11 | T038–T040 | Detail view, pipeline, contacts | ❔ | |
| PR-12 | T041–T043 | Audit history, activity stream, settings | ❔ | |
| PR-13 | T044–T046 | Contact provenance, role accounts | ❔ | |
| PR-14 | T047–T049 | Templates, draft preview, truthfulness guard | ❔ | |
| PR-15 | T050–T052 | Sandboxed send, suppression | ❔ | No email provider key or verified sending domain. |
| PR-16 | T053–T055 | Campaigns, sequences, compliance gate | ❔ | |
| PR-17 | T056–T058 | Scheduling, lock, quiet hours | ❔ | "Run now" and UTC quiet hours were broken; fixed by N01 and N02. |
| PR-18 | T059–T061 | Failure classification, backoff | ❔ | |
| PR-19 | T062–T064 | Test depth, scoring, fixtures | ❔ | |
| PR-20 | T065–T067 | Browser E2E, accessibility, SSRF/XSS | ❔ | |
| PR-21 | T068–T070 | Deployment, runbook, handover | ❌ | Never deployed. Covered by N06–N08. |

## Pull requests merged on 2026-11-22 (into `develop`, merge commits)

| Group | Backend PR | Frontend PR | Content |
|---|---|---|---|
| PR-22 | #27 | – | Persistence, worker, tracker |
| PR-23 | #28 | #30 | Remove unused code |
| PR-24 | #29 | #31 | Split oversized files |
| PR-25 | #30 | – | 2-hour shift agents, run-due endpoint |
| PR-26 | #31 | #32 | Security hardening |
| PR-27 / PR-28 | #35 | #35 | 8-agent relay + Agents workspace |
| PR-29 | #33 | – | Auto-merge rule |
| PR-30 | #34 | #34 | Development agents installed |
| PR-31 / PR-33 | #36 | #36 | Lead heat map |
| PR-32 | #37 | – | Password hashing |
| PR-34 | – | #37 | Sign-in redesign |
| PR-35 | this PR | this PR | Docs consolidation |

Release PRs `develop` → `main` (open, not merged: each has a "Before merging" item): backend #32, frontend #33.

## Waiting on you

1. Supabase connection string (N06) and the Render secrets listed in `docs/DEPLOYMENT.md`.
2. Your keywords / draft threshold for scoring (N27).
3. Approval to upgrade the vulnerable package (N28).

---

## Daily log

### 2026-11-22

- Merged to `develop`: PR-22 to PR-34 (persistence, 2-hour shift agents, 8-agent relay with live feed, heat map, security hardening, password hashing, sign-in redesign, development agents installed). Combined result: 194 backend tests, 82 frontend tests.
- Live local check: 26 postings read, 9 dropped as not software work, 17 new leads, 20 s.
- Docs consolidated (PR-35). Notion not updated (blocked by `Codebase/AGENTS.md`).
- A type-check command used `npx` and downloaded an unrelated package named `tsc` (it only printed a banner). Reported to the owner; only project-local tools are used from now on.

### 2026-11-21

- Found why the app showed nothing: the backend was never deployed, data lived in memory, and the only enabled agent ran once a day on a placeholder source.
- Replaced four outdated trackers with this file.
