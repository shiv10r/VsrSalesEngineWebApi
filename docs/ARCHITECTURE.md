# VSR Sales Engine — Architecture

Status: matches the code merged into `develop` on 2026-11-22. Import this file into Notion
(Import → Markdown) if you want it there; the project rules do not let agents write to Notion.

## 1. What it is

A lead-hunting tool for a software services company. A team of eight agents reads live job boards
every two hours, drops what is not software work, removes duplicates, scores each lead with listed
rules, finds public contact emails, drafts outreach for strong leads and checks compliance. A person
approves before anything is sent. Nothing is sent today (test mode).

## 2. System

```text
GitHub Actions timer (every 2 hours)
        |  POST /api/agents/run-due   (header X-Agent-Key)
        v
Render web service: .NET 10 API + in-process worker  <---->  Supabase PostgreSQL
        |
        +--> live sources: Hacker News, We Work Remotely, Remotive, Jobicy
        ^
        |  JWT, JSON over HTTPS
Vercel/Netlify: React + TypeScript frontend
```

Free-tier fit: Render sleeps after 15 minutes without traffic, so the timer wakes it. Each run keeps it
awake about 15 minutes, roughly 90 of the 750 free hours a month. Do not use Render's free Postgres
(deleted after 30 days); use Supabase. See `docs/DEPLOYMENT.md`.

## 3. The agent relay

Every run (timer, background worker or "Run agents now") passes through the same eight steps. Each
step writes readable events to the database as it works; the dashboard shows them live.

| Order | Agent | Job | Hands over |
|---|---|---|---|
| 1 | Aarav (Scout) | Reads each selected live source | raw postings |
| 2 | Priya (Filter) | Drops postings that are not software work, with a reason | software postings |
| 3 | Daniel (Dedupe) | Skips postings already stored | new leads |
| 4 | Ananya (Scorer) | Scores 0-100 using listed rules (contract work, .NET, modern stack, your keywords, public email, remote) | scored leads |
| 5 | Rohan (Contact Finder) | Looks for a public email inside the posting; never automates LinkedIn | leads with contact |
| 6 | Sophie (Drafter) | Drafts outreach for leads scoring over 50 (grounded, truthfulness guard) | drafts |
| 7 | Meera (Compliance) | Checks the suppression list and opt-out footer on each draft | approved drafts |
| 8 | James (Reporter) | Writes the run summary and counts | run record |

Rules-based, not an AI model, unless an LLM key is configured. Pacing between events is
`Agents:StepPauseMs` (UI only, does not change results).

**Shifts (India time):** Morning 06:00-14:00 (Hacker News, Remotive), Day 14:00-22:00 (We Work Remotely,
Jobicy), Night 22:00-06:00 (all four). Each runs every 2 hours inside its window.

## 4. Backend

```text
Controller (thin: routing, auth, status codes)
   -> Service (rules, validation, the relay steps)
   -> Repository (EF Core queries and persistence)
   -> PostgreSQL (Supabase) or EF InMemory for local dev
```

A test enforces that controllers never touch repositories or the database context directly.
Projects: `src/VsrSalesEngineWebApi` (host, controllers, workers, security), `.Core` (services,
interfaces, DTOs, entities), `.Infrastructure` (EF Core, repositories, live feed clients).
Files stay under 300 lines.

**Main tables:** opportunities, contacts, search profiles, source registry, campaigns, outreach
templates, suppressions, schedules, executions, distributed locks, `AgentRuns`, `AgentEvents`, users.

**Endpoints (agents):**

| Endpoint | Auth | Purpose |
|---|---|---|
| `POST /api/agents/run-due` | `X-Agent-Key` | Timer: run every schedule that is due |
| `POST /api/agents/run-now` | signed-in, not guest | Start a run now (202, 409 if running) |
| `GET /api/agents/overview` | signed-in | Agent roster, status, last run |
| `GET /api/agents/runs` | signed-in | Run history |
| `GET /api/agents/events` | signed-in | Activity feed (oldest first, `afterId` paging) |
| `GET /api/agents/heatmap` | signed-in | Leads and runs by weekday and hour (India time) |
| `GET /api/agents/status` | signed-in | Schedule status |

Other controllers: auth, opportunities, contacts, campaigns, outreach, schedules, search profiles,
sources, reliability, AI forms, health.

## 5. Frontend

React + TypeScript + Vite. `src/App.tsx` is a thin shell. Hooks hold state per area
(`useOpportunities`, `useAgents`, `useHeatmap`, `useSignIn`, ...), one component per screen under
`components/screens`, one API module per feature under `services`. Files stay under 250 lines.

Pages: Dashboard (live agent activity, lead heat map), Agents (roster, live feed, run history),
Opportunities, Contacts, Campaigns, Outreach, Schedules, Sources, Reliability, Settings, Sign in.
Every number on screen comes from the API; empty and unreachable states say so plainly.

## 6. Security

- **Sign-in:** JWT. Passwords hashed with PBKDF2-SHA256 (100,000 iterations, per-user salt). Demo users
  exist only in Development; elsewhere an admin is created only from `Seed__AdminEmail` /
  `Seed__AdminPassword`.
- **Guests:** read-only. A guard blocks guest tokens on every data-changing endpoint, and a test fails
  if a new one is left open.
- **Startup check:** outside Development the app refuses to start with a placeholder or short JWT secret.
- **Rate limits:** 300 requests a minute per client, 10 a minute on sign-in (429 beyond).
- **Headers:** nosniff, frame deny, no-store, CSP, HSTS in production; generic 500 messages.
- **Frontend:** CSP and security headers on Vercel and Netlify; the demo-login shortcut and password are
  not in production builds.
- **Timer endpoint:** shared secret, constant-time comparison, disabled until `Agents__RunKey` is set.

## 7. Boundaries

- Outreach is draft-first and in test mode until a sending provider key and a verified sending domain exist.
- LinkedIn is manual handoff only. No automated connection requests or messages.
- Restricted sources are never fetched. Each lead keeps its source link and retrieval time.
- Vendor claims (GST, certificates, references) are never invented.

## 8. Known gaps

- Backend not deployed; Supabase not connected (tasks N06-N08).
- Scores are low on live data (top 20 so far); draft step rarely triggers (N27).
- A database that already exists needs a migration for `AgentRuns` and `AgentEvents` (N26).
- Several older sources (company careers, public procurement) still generate placeholder leads.
