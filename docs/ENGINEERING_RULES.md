# Engineering Rules

One place for the project rules. Merged from the original UI-pack documents (architecture rules, branch
policy, QA checklist) on 2026-11-22. Agent-specific rules, including auto-merge, are in `agents/README.md`.

## Layering

```text
React UI  --HTTP + DTOs + JWT-->  Controller  ->  Service (BLL)  ->  Repository  ->  EF Core / PostgreSQL
```

Controllers stay thin and never touch the database context or repositories. Services do not depend on HTTP
types. Repositories do not build HTTP responses. Dependency injection connects interfaces to implementations.
Backend files stay under 300 lines, React files under 250, one responsibility each.

## Authentication and guests

JWT is the production path, validated in the backend (never only in React). Guest mode is for local
development only: `ASPNETCORE_ENVIRONMENT=Development` plus `Features:AllowDevelopmentGuest=true`; it fails
closed everywhere else. Guests are read-only: they use seeded data and cannot send, deploy, run agents or
change data. Passwords are hashed; secrets never go in source or build output.

## Sources and outreach

- Prefer documented APIs, published feeds, permitted public access and human handoff. No restricted
  scraping, login or CAPTCHA bypass, or direct-message automation (LinkedIn is manual handoff only).
- Every lead keeps its source link and retrieval time. Runs, schedule state, retries, errors and
  per-result provenance are stored. Duplicates are removed deterministically.
- Outreach is draft-first. Before any real send check: channel configuration, authorization,
  suppression/opt-out, rate caps, duplicate-send protection and an audit event. An unclear provider
  response never triggers a blind resend.
- A source failure or missing API is reported as such, never as fake success or invented results.

## Vendor readiness

Sales relevance, qualification and vendor/document readiness are separate. A buyer requirement points to
evidence for a specific opportunity. Never claim VSR GST registration, offices, certificates, customer
references or documents that are not real. Only an authorised human marks a sensitive document verified.

## Branches and PRs

`feature/PR-NN-Txxx-name` (or `fix/`, `chore/`, `docs/`) -> PR into `develop` -> a separate
"Release PR-NN" PR from `develop` into `main`. Never commit directly to `develop` or `main`, never skip
`develop`, never bypass CI. Merging follows the standing auto-merge rule in `agents/README.md`
(checks green and mergeable; release PRs wait while a "Before merging" item is open).

## Done means checked

A task is Done only when its acceptance criteria pass and the evidence is real. Never fabricate test
output, PR status, deployment status or data. If something is blocked (credentials, a human decision),
say so, do the independent work, and do not mark it Done.

**Per page:** matches the shared shell and tokens; checked at desktop and phone width; keyboard and focus
work; loading, empty, error and success states exist; no console errors or horizontal overflow; the UI
never claims an operation the API has not confirmed.

**Release gate:** lint, type-check and build pass; unit and integration tests pass; a browser smoke test of
the main journeys passes; review findings resolved; the PR to `develop` carries its checks; preview and
production configuration checked with no secrets in the build output; `TASKS.md` updated with true blockers.
