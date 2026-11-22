# VSR Sales Engine — Task Tracker

**Purpose (set by the owner, 2026-11-22): this app is a lead generator.** It finds local business leads in
Uttar Pradesh districts in real time (Google Maps via the official Places API, CSV imports, manual adds) and
people connected to job posts. It does not send email, run campaigns or post on LinkedIn.

The only task tracker for both repos. Daily log at the bottom. **Last updated:** 2026-11-22

## Count

| Status | Count |
|---|---|
| ✅ Done and checked | 22 |
| 🟡 In progress or partly done | 8 |
| ⬜ Not started | 11 |
| ⛔ Blocked, dropped or removed (out of scope) | 10 |
| ❔ Cannot tell | 1 |
| **Total** | **52** |

Key: ✅ checked working · 🟡 started or partly true · ⬜ not started · ⛔ blocked, dropped or removed ·
❔ not verifiable yet. Update the table whenever a row changes.

## Done (merged into `develop`)

| # | What | PR |
|---|---|---|
| D01 | Real database storage and background worker | 22 |
| D02 | Agent relay with live activity feed, Agents page, dashboard panel | 27, 28 |
| D03 | Agent settings panel (names, on/off, keywords, shifts) | 36, 37 |
| D04 | Security: headers, rate limits, guest guard, password hashing, JWT secret check, CSP | 26, 32 |
| D05 | New sign-in screen and new sidebar | 34, 45 |
| D06 | Lead heat map on the Dashboard | 31, 33 |
| D07 | Local Leads: 75 UP districts, 10 categories, CSV import, manual add | 38, 39, 44 |
| D08 | People finder (anyone connected to a post) and manual LinkedIn handoff | 40, 41 |
| D09 | Real-time Google Maps search through the official Places API (needs a key, G03) | 48, 49 |
| D10 | Fake data, simulated leads and false status claims removed | 46, 47 |
| D11 | Markdown docs consolidated, one tracker | 35 |
| D12 | Development agents installed in `.claude/agents` of both repos | 30 |

## In progress

| # | What | Status | Notes |
|---|---|---|---|
| N29 | Trim to a pure lead generator: delete outreach sending, campaigns, kill switch, reliability drills, vendor readiness, AI form assistant; relay becomes 6 agents | 🟡 | PR-50 backend, PR-51 frontend. Also adds a global "sign-in required" rule, an exact-origin CORS list and safe link handling. |
| N30 | Security review | 🟡 | Done. Two critical items are live today (open AI and kill-switch endpoints); N29 deletes them. The rest are in N31. |

## Next, in order

| # | What | Status | Notes |
|---|---|---|---|
| N31 | Production safety: refuse to start in production without a database; fix lock and run races; stop trusting any forwarded header; per-account sign-in lockout; stronger password hashing (600,000 rounds); persist the Places daily cap; pin the vulnerable packages | ⬜ | From the code and security reviews. |
| N32 | **Maps Scout agent:** a saved search plan (district × category × query) that runs daily on the official Places API and saves new places as leads | ⬜ | Needs G03. Saves only place ID, name, district, category and Maps link (Google's storage terms). |
| N33 | Export leads to CSV | ⬜ | |
| N34 | Launch the agents at a fixed daily time you choose, in India time | ⬜ | Needs your times. |
| N35 | HTTP-level tests: sign-in, run-due, Local Leads, settings (none exist today) | ⬜ | |
| N36 | Logo in the sidebar, sign-in screen and browser tab | ⬜ | Needs your file at `VsrSalesEngineWebapp/public/logo.png`. |
| N37 | Upgrade the vulnerable `System.Security.Cryptography.Xml` / `Pkcs` packages | ⬜ | Needs your approval (installs packages). They are design-time only, not shipped. |
| N38 | Database migrations for a database that already has tables | ⬜ | Only if you keep a populated database. |

## Needs you (go-live)

| # | What | Status |
|---|---|---|
| G01 | Supabase connection string as `ConnectionStrings__DefaultConnection` on Render (the live server still uses memory and loses data on restart) | ⬜ |
| G02 | Vercel: set `VITE_API_BASE_URL` to `https://vsrsalesenginewebapi.onrender.com` and redeploy | ⬜ |
| G03 | Google Cloud: enable Places API (New), create a restricted key, set a budget alert, set `GooglePlaces__ApiKey` on Render | ⬜ |

## Dropped or removed (out of scope)

| # | What | Why |
|---|---|---|
| X01 | Outreach sending, campaigns, kill switch, reliability drills, vendor readiness, AI form assistant | Not lead generation. They held most of the fake code. Removed in N29. |
| X02 | LinkedIn daily-post agent | Not lead generation. |
| X03 | Scraping Facebook, Instagram, YouTube, Google Maps, Justdial | Forbidden by their terms and risks your accounts. The official Places API and your own lists are used instead. |
| X04 | Automatic LinkedIn connection requests and messages | LinkedIn forbids it. Manual handoff links only. |
| X05 | Publishing to Notion | `Codebase/AGENTS.md` forbids outside services. Import `docs/ARCHITECTURE.md` into Notion yourself. |

## Earlier plan (T001–T070, PR-00 to PR-21), checked against the code on 2026-11-22

| PR | Scope | Status | Evidence |
|---|---|---|---|
| PR-00 | Baseline, source policies | ✅ | Source registry with policy status; `docs/ENGINEERING_RULES.md` |
| PR-01 | Solution skeletons | ✅ | Builds; 3 projects plus tests |
| PR-02 | Worker, Docker, CI | ✅ | Files exist and CI runs; Docker image built by Render |
| PR-03 | PostgreSQL entities and repositories | ❔ | Works in memory; Npgsql never run against real data (G01) |
| PR-04 | Auth, JWT, guest gate, audit | 🟡 | Auth and guest gate verified; no audit history exists |
| PR-05 | Source contracts, discovery | ✅ | Contract fixture tests |
| PR-06 | Source adapters | 🟡 | Fakes removed; real adapters: 4 job feeds (off) and Google Places |
| PR-07 | Dedupe, search profiles | ✅ | Dedup keys and profile endpoints |
| PR-08 | Qualification and scoring | ✅ | Scoring rules and role filter, tested |
| PR-09 | Vendor readiness | ⛔ | Removed in N29 |
| PR-10 | Dashboard, discovery runner | ✅ | Fake chart and numbers removed |
| PR-11 | Detail view, pipeline, contacts | ✅ | Tested |
| PR-12 | Audit history, activity stream, settings | 🟡 | Activity stream and settings exist; no audit history |
| PR-13 | Contact provenance, role accounts | ✅ | Classifier and tests |
| PR-14 | Templates, draft preview | ⛔ | Removed in N29 |
| PR-15 | Sandboxed send, suppression | ⛔ | Removed in N29 (the send was a fake) |
| PR-16 | Campaigns, compliance gate | ⛔ | Removed in N29 |
| PR-17 | Scheduling, lock, quiet hours | ✅ | Tested |
| PR-18 | Failure classification, backoff | ⛔ | Removed in N29 |
| PR-19 | Test depth, fixtures | 🟡 | 477 backend and 316 frontend tests; some were network-dependent or only asserted literals (being fixed in N29) |
| PR-20 | Browser E2E, accessibility, SSRF/XSS | 🟡 | SSRF protection is real and tested; there is no browser E2E |
| PR-21 | Deployment, runbook | 🟡 | Backend is live on Render and the frontend on Vercel; no database yet (G01) |

## Pull requests merged into `develop`

PR-22 to PR-49 are merged; see the Done table. Release PRs `develop` → `main` are made after each batch
and merged once checks pass and no "Before merging" item is open.

---

## Daily log

### 2026-11-22

- Merged PR-22 to PR-49: database, 8-agent relay with live feed, agent settings, security hardening,
  sign-in and sidebar redesigns, heat map, Local Leads, people finder, Google Maps search (official API),
  removal of fake data. Backend deployed on Render, frontend on Vercel.
- Owner decided the purpose: lead generator only. Code review, security review and QA audit ran in parallel.
  Findings: two critical open endpoints (AI, kill switch), a fake outreach "send", fabricated defaults in the
  AI form, state kept only in memory. N29 removes the features that hold them.
- A stale 2,789-line `App.tsx` in the frontend working folder was backed up and replaced by the committed one.
- A type-check command used `npx` and downloaded an unrelated package named `tsc` (reported earlier). Only
  project tools are used now.

### 2026-11-21

- Found why the app showed nothing: backend never deployed, data in memory, one agent on a placeholder source.
- Replaced four outdated trackers with this file.
