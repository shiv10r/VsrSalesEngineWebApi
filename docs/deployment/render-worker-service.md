# Render Background Worker & Service Deployment Guide (T057)

## Overview
This document specifies the operational design, service configuration, and cost/resource assumptions for deploying the VSR Sales Engine Web API and Background Worker on Render.

## Architecture

- **Web API Service**: ASP.NET Core 10 LTS web service handling frontend requests, authentication, and state management.
- **Worker Service**: Render Background Worker (`type: worker`) continuously evaluating recurring automation schedules, executing discovery adapters, tracking retry backoff, and processing scheduled follow-up steps.
- **Distributed Concurrency Lock**: Worker processes and manual web API triggers compete for atomic leases using `IDistributedLockService`. If a worker instance is currently executing a schedule, all parallel manual and background workers abort execution safely with `Conflict` (no duplicate scraping or outreach).

## Resource Limits & Plan Assumptions

| Parameter | Starter Plan (Recommended) | Free Plan (Non-viable for Worker) |
|---|---|---|
| Service Type | Background Worker (`worker`) | Web service only (sleeps after 15m) |
| CPU | 0.5 CPU core | 0.1 CPU core shared |
| RAM | 512 MB | 512 MB |
| Monthly Cost | ~$7 / month per service | $0 |
| Sleep behavior | Always-on, no spinning down | Spins down after 15 minutes of inactivity |
| Schedule Accuracy | High (continuous loop with 10s tick) | Unpredictable (sleep disrupts cron timers) |

## Build & Run Commands

- **Build Command**:
  ```bash
  dotnet publish src/VsrSalesEngineWebApi/VsrSalesEngineWebApi.csproj -c Release -o out
  ```
- **Web API Start Command**:
  ```bash
  dotnet out/VsrSalesEngineWebApi.dll
  ```
- **Worker Start Command**:
  ```bash
  dotnet out/VsrSalesEngineWebApi.dll --worker
  ```

## Required Environment Variables Checklist

- `ASPNETCORE_ENVIRONMENT`: `Production`
- `OUTREACH_MODE`: `Test` (fail-closed test sink routing until production compliance readiness gate passes)
- `ConnectionStrings__DefaultConnection`: Supabase PostgreSQL pooled connection URI
- `Jwt__Issuer`: `VsrSalesEngineApi`
- `Jwt__Audience`: `VsrSalesEngineClients`
- `Jwt__SecretKey`: Min 32-byte production signing key
- `AllowedOrigins`: Netlify frontend origin (e.g. `https://vsrsalesengine.netlify.app`)
