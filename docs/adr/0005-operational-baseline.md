# ADR 0005 — Operational baseline: explicit migrations, health model, deny-by-default HTTP

- Status: Accepted
- Date: 2026-10-13

## Decisions

- **Migrations run as an explicit step** (`dotnet NotifyHub.Api.dll migrate`, the `migrate` service in Compose), not at
  API start-up. API and worker containers start only after it exits successfully. EF Core serialises concurrent migration
  runs. The readiness check reports *Unhealthy* if the schema is behind the code.
- **Health endpoints:** `/health/live` (process only, no dependencies) and `/health/ready` (database, schema, worker
  heartbeat). A stale/missing worker heartbeat is *Degraded* (HTTP 200) rather than *Unhealthy*, because the API can still
  serve requests. Responses contain only status names and durations — never exception text or configuration (tested).
- **Worker liveness** is a database heartbeat row per instance (upsert) plus a local file for the container probe.
- **Authorisation is deny-by-default:** a fallback policy requires an authenticated user for every request, including
  unmatched routes. Until real schemes exist, a fail-closed default scheme authenticates nobody, so everything except the
  health and OpenAPI endpoints returns 401 problem details.
- **Structured JSON logs** to stdout with UTC timestamps and scopes; a correlation ID (validated inbound
  `X-Correlation-ID` or generated) is attached to every log scope, response header and problem-details body.
- **Runtime images** run as the non-root `app` user; health probes are implemented as `healthcheck` sub-commands because the
  base images contain no curl.

## Consequences

- An extra deployment step, but schema changes are observable and can be gated (backup first — Phase 5).
- EF Core logs one "Failed executing DbCommand" error on a brand-new database while probing the not-yet-created migrations
  history table. This is expected and harmless; command failures are deliberately not suppressed.
