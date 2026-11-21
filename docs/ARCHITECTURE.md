# VSR Sales Engine — Architecture (draft for review)

Status: DRAFT, 2026-11-21. Nothing here is built yet except the database and worker on the local branch.

## 1. Big picture

```
GitHub Actions timer (every 1-2h)
        |  POST /api/agents/run-due  (secret key)
        v
Render web service (.NET 10 API)  <---->  Supabase Postgres
   |  Controllers (thin)
   |  Services (rules)
   |  Repositories (data)
   v
Live sources: Hacker News, We Work Remotely, Remotive, Jobicy
        ^
        |  reads
Vercel/Netlify frontend (React): dashboard, agents screen, leads, campaigns
```

Free-tier fit: Render sleeps after 15 min idle, so the timer wakes it; each run uses ~15 min awake.
Do not use Render's free Postgres (deleted after 30 days); use Supabase.

## 2. The in-app agent team (what you will see working together)

Each hourly run is a relay. Every step is one agent, writes its result to the database, and
hands off to the next. The Agents screen shows each step live.

| Step | Agent | Does | Hands off |
|---|---|---|---|
| 1 | Scout (one per source / shift) | Fetches new postings from its live source | raw leads |
| 2 | Dedupe | Removes leads already stored | new leads |
| 3 | Scorer | Scores fit (skills, budget, freshness) | scored leads |
| 4 | Contact finder | Pulls public contact email only if the posting shows one | leads + contact |
| 5 | Drafter | Writes a grounded outreach draft (Gemini) | drafts for approval |
| 6 | Compliance gate | Checks suppression list, opt-out, source terms | approved drafts |
| 7 | Reporter | Writes a run summary for the dashboard | run record |

Outreach stays approval-required: agents draft, a person sends.

Shifts: Scouts are grouped into shifts (morning / day / night) with windows in India time.
Each schedule has "every N hours" and only runs inside its window.

## 3. Backend layers (repository pattern + MVC)

- `Controllers/` thin: validate input, call one service, return result.
- `Core/Services/` business rules, no database code.
- `Core/Interfaces/` repository and service contracts.
- `Infrastructure/Repositories/` all data access (EF Core), one repository per aggregate.
- `Infrastructure/Agents/` one small class per agent step (limit 300 lines per file).
- `Workers/` the hosted worker still runs; the GitHub timer endpoint calls the same code.

## 4. Data added for agents

- `AgentRun` (id, schedule, started, finished, status, counts per step)
- `AgentStepLog` (run id, agent name, started, finished, items in/out, message)
Both feed the Agents screen and the dashboard.

## 5. Endpoints to add

- `POST /api/agents/run-due` (secret key) runs every due schedule.
- `GET /api/agents/runs` latest runs with step logs.
- `GET /api/agents/status` each agent's last run and result.

## 6. Frontend

Split `App.tsx` (2,789 lines) into screens, components, hooks and one API file per feature, each
under 250 lines. New screens: Dashboard (new leads, last run), Agents (relay view), Leads, Campaigns.

## 7. Delivery order (new branch per PR: feature -> develop -> main)

1. Architecture sign-off (this file)
2. UI mockup sign-off
3. Agent data + run-due endpoint + shifts
4. Repository/MVC refactor and file splitting
5. Agents screen and new dashboard
6. Supabase, push, Render deploy, GitHub timer, frontend URL
