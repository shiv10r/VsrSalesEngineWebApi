# VSR Sales Engine — Web API

Backend API and background worker for VSR Sales Engine built with .NET 10 LTS and ASP.NET Core.

## Architecture

This service strictly implements the required layered flow:
```text
Controller  (thin HTTP routing, auth policies, status codes)
   ↓
BLL / Service Layer  (business logic, validation, orchestrations)
   ↓
Repository Layer  (data queries and persistence)
   ↓
Database  (Supabase PostgreSQL / local PostgreSQL)
```

- Controllers are thin HTTP adapters and never access repositories or DbContext directly.
- Business Logic Layer (BLL) owns validation and security gates.
- Repository layer owns data access.
- DTOs are decoupled from domain entities.

## Authentication & Guest Mode

- **JWT Authentication** is the default production authentication method.
- **Development Guest Mode**:
  - Gated behind `ASPNETCORE_ENVIRONMENT=Development` AND `Features:AllowDevelopmentGuest=true`.
  - Fail-closed in Production environments even if client bypass tokens are forged.
  - Restricted permissions: Guest mode only provides seeded sample data and cannot trigger real outreach or provider integration.

## Solution Projects

- `src/VsrSalesEngineWebApi`: Web API host, Controllers, Middleware, Auth Policies
- `src/VsrSalesEngineWebApi.Core`: Business logic (BLL), interfaces, DTOs, domain models
- `src/VsrSalesEngineWebApi.Infrastructure`: Data access, EF Core context, repository implementations
- `tests/VsrSalesEngineWebApi.Tests`: Unit and integration test suite

## Running Locally

```bash
dotnet build
dotnet test
dotnet run --project src/VsrSalesEngineWebApi
```
