# VSR Sales Engine — Web API

Backend for the VSR Sales Engine lead generator: .NET 10 LTS, ASP.NET Core, EF Core, PostgreSQL.
It finds local business leads in Uttar Pradesh in real time (Google Maps Places) and, optionally, job-posting
leads: a team of six agents reads live job boards every two hours, then filters, de-duplicates, scores and
finds contacts. It never sends messages. Frontend: [VsrSalesEngineWebapp](https://github.com/shiv10r/VsrSalesEngineWebapp).

## Run locally

```bash
dotnet build
dotnet test
dotnet run --project src/VsrSalesEngineWebApi
```

With no connection string the app uses an in-memory database (data resets on restart). In Development it
seeds two demo users: `sales@vsrsalesengine.com` / `Sales123!` and `admin@vsrsalesengine.com` / `Admin123!`.
They exist only in Development. Sign in, open Agents, click "Run agents now".

## Layout

- `src/VsrSalesEngineWebApi` — host, controllers, workers, security
- `src/VsrSalesEngineWebApi.Core` — services, agent relay, interfaces, DTOs, entities
- `src/VsrSalesEngineWebApi.Infrastructure` — EF Core, repositories, live feed clients
- `tests/VsrSalesEngineWebApi.Tests` — unit and integration tests

## Docs

| File | What |
|---|---|
| [TASKS.md](TASKS.md) | The one task tracker, with the daily log |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | System, agent relay, endpoints, security |
| [docs/ENGINEERING_RULES.md](docs/ENGINEERING_RULES.md) | Layering, guest mode, sources, branches, definition of done |
| [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) | Render, Supabase, timer, environment variables, go-live checklist |
| [agents/README.md](agents/README.md) | Rules for the development agents in `.claude/agents` |
