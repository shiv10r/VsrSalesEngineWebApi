# VSR Sales Engine - Operational Runbook & Smoke Test Drill

**Document ID:** `VSR-OPS-069`  
**Phase:** Phase 8 - Hardening, Packaging & Release  
**Tickets Covered:** `T068`, `T069`, `T070` (PR-21)  
**Classification:** Operational Procedure & Disaster Recovery Guide  
**Status:** Approved for Production Candidate

---

## 1. Overview & Objectives

This runbook defines the standard operational procedures, pre-flight deployment verification checklist, smoke test drills, database migration/rollback guides, and emergency kill-switch incident management protocols for the VSR Sales Engine platform.

The system comprises:
1. **Frontend Webapp:** Single-Page Application (React 18, Vite, TypeScript) deployed to Netlify.
2. **Backend Web API:** .NET 10 LTS Web API deployed to Render Web Service.
3. **Background Worker:** .NET 10 LTS Idempotent Discovery & Schedule Worker deployed to Render Background Worker.
4. **Database:** Supabase Managed PostgreSQL with transaction connection pooling.

---

## 2. Pre-Deployment Environment Checklist

Before triggering a production deployment or promoting from staging, confirm the presence and validity of the following environment configuration variables:

| Variable | Target Service | Permitted Values | Default / Fallback | Safety Impact |
|---|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | WebApi / Worker | `Production`, `Staging`, `Development` | `Production` | Controls error disclosures & Swagger availability |
| `ConnectionStrings__DefaultConnection` | WebApi / Worker | Supabase PostgreSQL URI (port 6543) | *Required* | Pooler connection string with SSL Mode=Require |
| `Jwt__SecretKey` | WebApi | Cryptographic 256-bit string (&ge;32 chars) | *Required* | Session token signing; never share or commit |
| `Jwt__Issuer` | WebApi | `VsrSalesEngineApi` | `VsrSalesEngineApi` | JWT token validation claim |
| `Jwt__Audience` | WebApi | `VsrSalesEngineClients` | `VsrSalesEngineClients` | JWT audience validation claim |
| `AllowedOrigins` | WebApi | Comma-separated CORS origins | `https://vsrsalesengine.netlify.app` | Prevents cross-origin unauthorized access |
| `OUTREACH_MODE` | WebApi / Worker | `Test` or `Production` | `Test` | **CRITICAL:** Defaults to `Test` (Sandbox Sink). Production requires explicit sign-off. |
| `OUTREACH_KILL_SWITCH` | WebApi / Worker | `false` or `true` | `false` | **EMERGENCY:** Immediately halts all outbound outreach dispatching. |
| `WORKER_CONCURRENCY_LIMIT` | Worker | Integer (&ge;1) | `1` | Enforces distributed lock to prevent overlapping runs |
| `ADZUNA_APP_ID` / `ADZUNA_APP_KEY` | WebApi / Worker | External API credentials | Optional / Fixture fallback | Approved public job source adapter credentials |
| `VITE_API_BASE_URL` | Webapp (Netlify) | `https://vsr-sales-engine-webapi.onrender.com` | `/api` | Public endpoint for API requests |

---

## 3. Production Smoke Test Drill Protocol (T069)

Execute this drill immediately following any production deployment or routine maintenance window.

### Step 1: Health Endpoint Probe
Probe the automated health check route:
```bash
curl -I -s -X GET "https://vsr-sales-engine-webapi.onrender.com/health"
```
**Expected Response:** `HTTP/1.1 200 OK`
```json
{
  "status": "Healthy",
  "service": "VsrSalesEngineWebApi",
  "environment": "Production",
  "framework": ".NET 10 LTS",
  "timestampUtc": "2026-10-18T10:30:00Z"
}
```

### Step 2: Automated Operational Drill Probe
Execute the automated smoke test endpoint:
```bash
curl -s -X GET "https://vsr-sales-engine-webapi.onrender.com/api/reliability/smoke-test"
```
**Pass Criteria:**
1. Overall `status` is `"PASS"`.
2. `allChecksPassed` is `true`.
3. All 4 diagnostic subsystems report `"PASS"`:
   - `DatabaseRepository`: Confirms persistent store read/write connectivity.
   - `SourceRegistry`: Confirms permitted discovery sources configured (`Permitted` status).
   - `ReliabilityEngine`: Confirms reliability metrics engine operational with zero dead letters.
   - `OutreachSafety`: Confirms `OUTREACH_MODE` is safe (`Test` mode sink active).

### Step 3: Source Outage & Graceful Degradation Drill
Simulate a degraded third-party source:
```bash
curl -s -X POST "https://vsr-sales-engine-webapi.onrender.com/api/reliability/simulate-failure" \
  -H "Content-Type: application/json" \
  -d '{"failureType": "Transient", "jobType": "DiscoveryRun", "referenceId": "SRC-DRILL"}'
```
**Verification:**
- System logs record transient failure with exponential backoff calculation.
- UI Reliability dashboard increments retryable failure counter.
- Worker process remains running with zero crashes or orphaned locks.

### Step 4: Outreach Safety & Sandbox Sink Verification
Trigger a test outreach dispatch from the UI or API:
```bash
curl -s -X POST "https://vsr-sales-engine-webapi.onrender.com/api/outreach/send" \
  -H "Content-Type: application/json" \
  -d '{
    "recipientEmail": "candidate.lead@example.com",
    "recipientName": "Candidate Lead",
    "subject": "Operational Drill Verification",
    "body": "Smoke test message payload.",
    "idempotencyKey": "SMOKE-DRILL-001",
    "isTestMode": true
  }'
```
**Pass Criteria:**
- Response returns `wasRoutedToSink: true` and `status: "SentToTestSink"`.
- `dev-sink@vsrsalesengine.local` receives the message.
- Target email (`candidate.lead@example.com`) is never contacted.

---

## 4. Emergency Kill-Switch Standard Operating Procedure (T069)

If anomalous activity, unexpected email volume, or compliance violations occur, engage the emergency kill switch immediately.

### Action A: Instant API Toggle (Sub-Second Halt)
Issue the toggle request to the reliability controller:
```bash
curl -s -X POST "https://vsr-sales-engine-webapi.onrender.com/api/reliability/kill-switch/toggle" \
  -H "Content-Type: application/json" \
  -d '{"engaged": true}'
```
**Confirmation Response:**
```json
{
  "isEngaged": true,
  "action": "KillSwitchEngaged",
  "message": "Operational kill switch engaged. Outbound sending halted.",
  "timestampUtc": "2026-10-18T10:35:00Z"
}
```

### Action B: Environment Variable Level (Persistent across Restarts)
1. Navigate to Render Dashboard &rarr; `vsr-sales-engine-webapi` &rarr; **Environment**.
2. Set `OUTREACH_KILL_SWITCH = true`.
3. Save changes. Both WebApi and Worker services will immediately halt all dispatch attempts, returning `KillSwitchEngaged` status and suppressing any outgoing email.

### Action C: Resuming Normal Operations
Once the incident is resolved:
1. Verify root cause analysis has concluded.
2. Ensure suppression lists and templates are validated.
3. Issue toggle request:
```bash
curl -s -X POST "https://vsr-sales-engine-webapi.onrender.com/api/reliability/kill-switch/toggle" \
  -H "Content-Type: application/json" \
  -d '{"engaged": false}'
```

---

## 5. Database Migration & Rollback Instructions

### Migration Strategy
Database schema migrations are executed against Supabase PostgreSQL using idempotent migration scripts stored in version control:

1. **Staging Verification:** Run migration scripts against the staging Supabase project first.
2. **Backup Pre-Flight:** Trigger a manual point-in-time snapshot in Supabase before running any DDL statement.
3. **Execution Order:**
   ```bash
   # Apply forward migration
   psql "$SUPABASE_DB_URL" -f migrations/001_initial_schema.sql
   psql "$SUPABASE_DB_URL" -f migrations/002_reliability_schedules.sql
   ```
4. **Smoke Verification:** Query the schema version table and trigger `/api/reliability/smoke-test`.

### Rollback Procedure
If a migration fails or introduces performance regression:
1. Immediately switch the application pool to Read-Only or activate Maintenance Mode.
2. Execute the corresponding reverse rollback script:
   ```bash
   psql "$SUPABASE_DB_URL" -f migrations/002_rollback.sql
   ```
3. If structural data corruption occurred, restore to the pre-migration snapshot via Supabase Dashboard &rarr; **Database** &rarr; **Backups** &rarr; **Restore to Point in Time**.

---

## 6. Backup & Restore Expectations

| Category | Policy | Target | RPO | RTO |
|---|---|---|---|---|
| Automated Daily Backup | Supabase Managed | AWS S3 multi-region | 24 Hours | < 15 Minutes |
| Point-in-Time Recovery (PITR) | Write-Ahead Log (WAL) Streaming | Supabase Enterprise WAL | 5 Minutes | < 30 Minutes |
| Pre-Release Snapshot | Manual Snapshot Trigger | Supabase Project Console | 0 Minutes | < 10 Minutes |

---

## 7. Operational Escalation Contacts

- **Lead Engineer / On-Call:** Engineering Team (`eng-oncall@vsrsalesengine.internal`)
- **Operations & Infrastructure:** DevOps (`devops@vsrsalesengine.internal`)
- **Compliance & Legal:** Compliance Review Board (`compliance@vsrsalesengine.internal`)
