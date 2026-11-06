# Render Deployment Guide

## Overview
This document specifies the operational design, service configuration, and cost/resource assumptions for deploying the VSR Sales Engine Web API on Render. `deployment/render.yaml` is the source of truth; this file explains the "why."

## Architecture

- **Web API Service**: ASP.NET Core 10 LTS web service handling frontend requests, authentication, and state management.
- **Autonomous Discovery Worker**: runs as an in-process `BackgroundService` (`DiscoveryWorkerHostedService`) inside the same web process. It polls enabled automation schedules every 60 seconds and, for any schedule whose `NextRunAtUtc` has passed, runs live discovery against the configured sources (Hacker News, We Work Remotely, Remotive, Jobicy, company careers feeds).
- **Persistence**: PostgreSQL (Supabase) via EF Core. Without `ConnectionStrings__DefaultConnection` set, the app falls back to an in-memory database for zero-config local development only — nothing persists across restarts in that mode.
- **Distributed Concurrency Lock**: `IDistributedLockService` uses a DB-backed row with an optimistic concurrency token, so if this service is ever scaled to multiple instances, only one instance executes a given schedule at a time (no duplicate scraping or outreach).

A separate Render worker service is intentionally **not** used — running the scheduler in-process keeps the architecture simple and halves hosting cost, while the DB-backed lock keeps it safe to scale out later if needed.

## Resource Limits & Plan Assumptions

| Parameter | Starter Plan (Recommended) | Free Plan (Not recommended) |
|---|---|---|
| CPU | 0.5 CPU core | 0.1 CPU core shared |
| RAM | 512 MB | 512 MB |
| Monthly Cost | ~$7/month | $0 |
| Sleep behavior | Always-on, no spinning down | Spins down after 15 minutes of inactivity — breaks the autonomous worker's polling loop |
| Schedule Accuracy | High (60s poll loop, always running) | Unpredictable (sleep disrupts the poll loop) |

## Build & Run Commands

- **Build Command**:
  ```bash
  dotnet publish src/VsrSalesEngineWebApi/VsrSalesEngineWebApi.csproj -c Release -o out
  ```
- **Start Command**:
  ```bash
  dotnet out/VsrSalesEngineWebApi.dll
  ```

## Required Environment Variables Checklist

- `ASPNETCORE_ENVIRONMENT`: `Production`
- `OUTREACH_MODE`: `Test` (fail-closed test sink routing until production compliance readiness gate passes)
- `ConnectionStrings__DefaultConnection`: Supabase PostgreSQL connection string, e.g. `Host=db.<project-ref>.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<password>;SSL Mode=Require;Trust Server Certificate=true`. Find it in the Supabase dashboard under Project Settings → Database → Connection string (use the "Connection pooling" URI on the Starter plan to avoid exhausting direct connections).
- `Jwt__Issuer`: `VsrSalesEngineApi`
- `Jwt__Audience`: `VsrSalesEngineClients`
- `Jwt__SecretKey`: Min 32-byte production signing key
- `AllowedOrigins`: Frontend origin (e.g. `https://vsrsalesengine.netlify.app`, `https://vsr-sales-engine-webapp.vercel.app`)

## First Deploy Checklist

1. Create the Supabase project and copy its pooled Postgres connection string.
2. In Render, create the service from `deployment/render.yaml` (Render Blueprint) or manually with the settings above.
3. Set `ConnectionStrings__DefaultConnection` in the Render dashboard (marked `sync: false` in the blueprint so it isn't committed to git).
4. On first boot, the app calls `EnsureCreated()` against that database and seeds baseline sources/templates/schedules if the tables are empty — no manual migration step is required for the initial schema.
5. Confirm `GET /health` returns 200, then set the frontend's `VITE_API_BASE_URL` to the Render service URL and redeploy the frontend.
