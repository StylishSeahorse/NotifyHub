# Phase 1 report — Repository and architecture foundation

Date: 2026-10-13

## 1. Starting point

The repository was **empty** (no commits, no files). There was no existing code, convention or build configuration to
preserve. The build environment had no .NET SDK; .NET 10.0.112 (LTS) was installed from the Ubuntu 24.04 archive because
Microsoft's installer host was blocked by the network policy. Docker, PostgreSQL client tools and NuGet were available.

## 2. Exit gate

| Criterion | Result |
|---|---|
| Project builds | ✅ `dotnet build -c Release`: 0 warnings, 0 errors (warnings are errors) |
| Migrations run | ✅ Applied to empty databases by the production migrator (tests, and in Docker Compose); `Down` to zero and back tested; idempotent SQL script applied twice cleanly |
| Health checks work | ✅ `/health/live`, `/health/ready` (database, schema, worker) tested in-process and validated through Caddy over HTTPS in Compose |
| Initial tests pass | ✅ 119 unit + 53 integration = **172 passed, 0 failed, 0 skipped** |

## 3. What was built

- **Toolchain:** `global.json` (SDK 10.0.1xx), `Directory.Build.props` (nullable, analyzers `latest-recommended`, warnings
  as errors, deterministic/CI builds), central package management with transitive pinning, pinned `dotnet-ef` local tool,
  `.editorconfig` enforced in build and by `dotnet format`.
- **Domain (`NotifyHub.Domain`, no dependencies):** tenants and branding; portal users, memberships and platform
  administrators; tenant-scoped recipients (AD SID / Entra / device-scoped local SID); devices (Pending → Active ⇄
  Quarantined → Revoked, key rotation); device groups; notification aggregate with the full lifecycle state machine,
  revisions/supersession, typed targets and bounded reminder policy; per-recipient state (dismiss, close-defers, acknowledge,
  activity-window check, timestamp clamping); deliveries; idempotent events; assets with server-generated storage keys;
  audit events.
- **Persistence (`NotifyHub.Infrastructure`):** EF Core 10 + Npgsql, snake_case, 17 tables, composite tenant foreign keys
  on every tenant-owned reference, CHECK constraints for enums and lifecycle consistency, uniqueness and idempotency
  constraints, partial indexes for scheduler/expiry/unresolved acknowledgements, `xmin` optimistic concurrency,
  append-only triggers on `audit_events` and `notification_events`.
- **API host:** JSON structured logging, correlation IDs, security headers, problem details, domain-error mapping,
  OpenAPI 3.1, 1 MB body limit, deny-by-default authorisation with a fail-closed scheme, `migrate` and `healthcheck`
  commands.
- **Worker host:** heartbeat service (database upsert + liveness file), failure-tolerant.
- **Deployment:** multi-stage Dockerfile (non-root runtime), Compose stack (PostgreSQL on an internal network, one-shot
  migration, API, worker, Caddy with automatic HTTPS/HSTS), `.env.example` with placeholders only.
- **CI:** GitHub Actions workflow — format check, Release build, unit and integration tests (Testcontainers), migration
  script generation, Compose validation and image builds. *Written but not executed* (no GitHub runner in this session).
- **Docs:** architecture, five ADRs, threat model, API contract status, development, deployment, operations.

## 4. Commands executed (all succeeded unless noted)

```text
apt-get install dotnet-sdk-10.0
dotnet tool restore
dotnet build NotifyHub.slnx [-c Release]
dotnet format NotifyHub.slnx --verify-no-changes
dotnet ef migrations add InitialSchema …
dotnet ef migrations script [--idempotent] …
dotnet test tests/NotifyHub.UnitTests            -> 119 passed
dotnet test tests/NotifyHub.IntegrationTests     -> 53 passed (Testcontainers, postgres:17.11-alpine)
docker build --target api|worker -f deploy/docker/Dockerfile .
docker compose -f deploy/docker-compose.yml --env-file <env> config|up -d|run --rm migrate|restart
curl -k https://localhost:8443/health/ready      -> 200 {"status":"Healthy",…}
curl -k https://localhost:8443/api/v1/notifications -> 401 problem+json
psql -f idempotent-migrations.sql (twice)        -> ok
caddy validate                                    -> Valid configuration
```

Failures encountered and fixed during the phase:

1. Unauthenticated requests to unknown routes returned 500 (fallback policy with no authentication scheme). Fixed with a
   fail-closed default scheme; now 401 problem details, covered by tests.
2. OpenAPI `servers` showed the internal `http://` URL behind Caddy. Fixed by emitting no server URLs; covered by a test.
3. Naming/format violations found by `dotnet format`; `.editorconfig` refined and code formatted.

## 5. Acceptance tests already proven at the domain/persistence level

Not end-to-end yet (no API workflows or Windows components), but the underlying rules are covered:

| Acceptance test | Coverage now |
|---|---|
| 1–3 Dismissal is per user and persists | Domain + persistence across DbContexts |
| 4–5 Critical re-appears; closing ≠ acknowledgement | Domain |
| 6–7 Acknowledgement stops reminders only for that user | Domain + persistence |
| 12 Cross-tenant references denied | Database constraints for every relationship |
| 14 Retried events do not duplicate | Idempotency constraint + idempotent transitions |
| 15 Expired notice not displayed | Domain (`IsActiveAt`, scheduler-late expiry) |
| 13, 8–11, 16 | Not yet — Phases 2–5 |

## 6. Limitations and honest status

- No REST resources, authentication schemes, SignalR, scheduler jobs or audit writing yet (Phase 2+).
- Row-level security is designed (ADR 0002) but **not enabled**; isolation currently relies on composite FKs plus
  future application filters.
- Windows service, agent, MSI and the React portal do not exist yet. Nothing about Windows session handling has been
  validated. This environment is Linux; Phase 3 needs real Windows machines.
- Reminder per-day caps, quiet hours and escalation are modelled as bounds only; enforcement needs tenant time zones.
- No backups, restore test, monitoring integration, least-privilege DB roles, image digests or signed artefacts (Phase 5).
- The CI workflow has not run on GitHub yet.
- EF Core logs one benign "Failed executing DbCommand" on first migration of a brand-new database (documented).

## 7. Security review of this phase

No secrets committed. The database is not published. Health and error responses leak no internals (tested). All requests
are denied unless an endpoint opts out. Domain input validation rejects control characters, oversize content, unsafe MIME
types and malformed SIDs/idempotency keys. Residual risks are listed in `threat-model.md`. No unresolved critical issues
were introduced; the main open item is that application-level tenant scoping and RLS must land with the first
tenant-scoped endpoint in Phase 2 — no tenant-scoped endpoint may be exposed before that.

## 8. Next phase — Phase 2: Backend and notification state

Scope: tenant context from authenticated membership, EF global query filters and PostgreSQL RLS with separate
migration/runtime roles; temporary development authentication for API tests (OIDC proper in Phase 4); tenant, notification,
targeting, device and group endpoints with role-based authorisation; audience calculation and preview; scheduler, expiry
and reminder jobs with concurrency-safe claiming; quiet hours/per-day caps with tenant time zones; audit event writing;
idempotent device event ingestion with transactional state transitions.

Exit criteria: recipient isolation, notification lifecycle and acknowledgement rules proven by automated API-level and
PostgreSQL integration tests, including cross-tenant attempts against every resource type and role-escalation attempts.

**Recommendation:** because Windows session identity is the highest-risk assumption (ADR 0003), consider running a small
Windows spike (service + per-session agent + pipe client-identity verification) in parallel with Phase 2, before the
device API contract is frozen.
