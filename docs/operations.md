# Operations (Phase 1)

## Health endpoints

| Endpoint | Meaning | HTTP |
|---|---|---|
| `GET /health/live` | Process is running (no dependency checks) | 200 |
| `GET /health/ready` | `database` reachable, `schema` has no pending migrations, `worker` heartbeat fresher than `Health:Worker:StaleAfter` (default 2 min) | 200 Healthy/Degraded, 503 Unhealthy |

Bodies contain only status names and durations. Container health checks run `dotnet NotifyHub.Api.dll healthcheck`
(calls `/health/live`) and `dotnet NotifyHub.Worker.dll healthcheck` (liveness file younger than 2 minutes).

## Logs

Both processes write JSON logs to stdout (`docker compose logs api worker`). Each API log entry carries a `CorrelationId`
scope, which is also returned as the `X-Correlation-ID` response header and in problem-details bodies.

Expected on first install: the `migrate` container logs one EF Core "Failed executing DbCommand" error while probing the
not-yet-existing `__ef_migrations_history` table, followed by "Database migrations applied". This is harmless.

## Inspecting worker heartbeats

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env exec postgres \
  psql -U notifyhub -d notifyhub -c "select * from worker_heartbeats order by last_heartbeat_at desc"
```

Backup/restore, monitoring of scheduler delay, device check-in, certificate expiry, disk and backup status are Phase 5.
