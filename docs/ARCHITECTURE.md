# VSR Sales Engine — Architecture

Status: matches the code merged into `develop` and `main` on 2026-11-22. Import this file into Notion
(Import → Markdown) if you want it there; the project rules do not let agents write to Notion.

## 1. Purpose

A **lead generator** for a software company. It finds local business leads in Uttar Pradesh districts in
real time and keeps them in one place, categorised by district and business type. It does not send email,
run campaigns or post on LinkedIn. Task status and what is next: `TASKS.md`.

## 2. System

```text
Vercel: React + TypeScript frontend
        |  JWT, JSON over HTTPS
        v
Render: .NET 10 API + in-process agent worker  <---->  Supabase PostgreSQL (not connected yet)
        |
        +--> Google Places API (New): real-time business search (needs GooglePlaces__ApiKey)
        +--> company websites (public pages only): people connected to a post
        +--> job feeds (Hacker News, We Work Remotely, Remotive, Jobicy): off by default
GitHub Actions timer (optional): POST /api/agents/run-due, wakes a sleeping free-plan server
```

## 3. Where leads come from

| Source | How | Stored |
|---|---|---|
| **Google Maps, live** | Official Places API search by district, category and text | Only on "Save as lead": name, district, category, place ID, Maps link. Phone, website and rating are fetched live (Google's storage terms) |
| CSV import | Your own lists, parsed in the browser | Name, district, category, phone, website, address, source label |
| Manual add | Form | What you type |
| Job postings (optional) | Four public job feeds, off by default | Posting, score, evidence link |
| People connected to a post | Poster, hiring people, decision-makers found on the company's own public pages | Name, title, source page, relation to the post |

Not used, by design: scraping Google Maps, Facebook, Instagram, LinkedIn or directories (their terms forbid
it). LinkedIn is manual handoff only: the app builds a search link and a message you copy and send yourself.

## 4. The agents

Every run (background worker, timer or "Run agents now") passes through six steps and writes readable events
to the database as it works; the Agents page shows them live.

| Order | Agent | Job |
|---|---|---|
| 1 | Aarav (Scout) | Reads each enabled source |
| 2 | Priya (Filter) | Drops postings that are not software work, with a reason |
| 3 | Daniel (Dedupe) | Skips items already stored |
| 4 | Ananya (Scorer) | Scores 0-100 using listed rules |
| 5 | Rohan (Contact Finder) | Looks for public contacts and people connected to the post |
| 6 | James (Reporter) | Writes the run summary and counts |

Names, on/off, keywords and shift windows are editable in Agent settings. Rules-based, not AI. With nothing
enabled a run finishes as "nothing to do". Planned: a Maps Scout that runs a saved district × category search
plan on the Places API every 30 minutes (task N32).

## 5. Backend

```text
Controller (thin) -> Service (rules) -> Repository (EF Core) -> PostgreSQL, or EF InMemory for local dev
```

A test enforces that controllers never touch repositories or the database context directly. Projects:
`src/VsrSalesEngineWebApi` (host, controllers, workers, security), `.Core` (services, relay, DTOs, entities),
`.Infrastructure` (EF Core, repositories, feed clients, public-page fetcher). File limit: 300 lines.

**Tables:** opportunities, contacts, local leads, search profiles, source registry, schedules, distributed
locks, agent runs, agent events, agent settings, users.

**Endpoints:**

| Area | Endpoints |
|---|---|
| Public | `GET /health`, `POST /api/auth/login`, `POST /api/auth/guest-session` (development only), `POST /api/agents/run-due` (key) |
| Local Leads | `GET /api/local-leads` (+ `/options`, `/summary`), `POST /api/local-leads` and `/import`, `PUT` and `DELETE /{id}`, `POST /discover` (live Maps search), `GET /discover/status`, `POST /from-place`, `GET /{id}/live-details` |
| Agents | `GET /api/agents/overview`, `/runs`, `/events`, `/heatmap`, `/status`; `GET` and `PUT /api/agents/settings`; `POST /api/agents/run-now` |
| Other | opportunities, contacts, schedules, search profiles, sources |

Every endpoint needs a signed-in user unless listed as public; a test fails if another one is reachable
without signing in.

## 6. Frontend

React + TypeScript + Vite. `src/App.tsx` is a thin shell; hooks hold state per area, one component per screen
under `components/screens`, one API module per feature under `services`. File limit: 250 lines.
Pages: Dashboard (agent activity, lead heat map), Agents (roster, live feed, run history, settings),
Local Leads, Opportunities, Discover, Contacts, Schedules, Sources, Activity log, Settings, Sign in.
Every number comes from the API; an empty or unreachable server shows an honest message, never sample data.

## 7. Security

- Sign-in: JWT; passwords hashed with PBKDF2-SHA256 and a per-user salt; in production the only user is the
  admin created from `Seed__AdminEmail` and `Seed__AdminPassword`.
- Global sign-in-required policy; guests (development only) are read-only and blocked from every endpoint that
  changes data or costs money; a reflection test enforces it.
- CORS allows exact origins only; no credentials mode; look-alike hosts are rejected.
- Rate limits: 300 requests a minute per client, 10 a minute on sign-in.
- Headers on the API (nosniff, frame deny, no-store, CSP, HSTS); CSP and headers on Vercel and Netlify.
- The server refuses to start in production with a placeholder or short JWT secret.
- Public-page fetcher: public addresses only, `robots.txt` honoured, size and request caps, no redirects off
  the site. The Places key is sent only in a header and never returned to the browser.

## 8. Known gaps

See `TASKS.md`. Main ones: Supabase not connected (the live server loses data on restart), no Maps Scout
agent or CSV export yet, a database that already exists needs a manual change after the scope trim, and
the server does not yet refuse to start in production without a database.
