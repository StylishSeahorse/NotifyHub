# Deployment (Phase 1 baseline)

> Phase 1 delivers a working, validated Compose stack. Backups, restore testing, upgrade/rollback procedures, monitoring
> integration and least-privilege database roles are Phase 5 and are **not yet implemented**. Do not use this for
> production data yet.

## Components

| Service | Image | Networks | Notes |
|---|---|---|---|
| `postgres` | `postgres:17.11-alpine` | backend (internal) | No published ports; `pgdata` volume |
| `migrate` | `notifyhub-api` | backend | One-shot `migrate` command; API/worker wait for it to succeed |
| `api` | `notifyhub-api` | backend, frontend | Listens on 8080 inside the network only |
| `worker` | `notifyhub-worker` | backend | Heartbeat; scheduler jobs from Phase 2 |
| `caddy` | `caddy:2.11.7` | frontend | Publishes 80/443, TLS, HSTS, WebSocket proxying |

The backend network is `internal: true`: PostgreSQL is unreachable from outside Docker, and the worker has no internet route.

## DNS and TLS

1. Create a DNS `A`/`AAAA` record for the chosen name (e.g. `notifyhub.example.com`) pointing at the server.
2. Allow inbound TCP 80 and 443 to the server (80 is needed for ACME HTTP-01 and HTTP→HTTPS redirects).
3. Set `NOTIFYHUB_DOMAIN` in `deploy/.env`. Caddy obtains and renews a publicly trusted certificate automatically.
4. For isolated networks without ACME, use `localhost` (Caddy internal CA, testing only) or mount your own certificate and
   add a `tls /path/cert.pem /path/key.pem` directive to the Caddyfile.

Windows devices only ever make outbound HTTPS connections to this name; no inbound access to workstations is required.

## First deployment

```bash
cp deploy/.env.example deploy/.env
# edit deploy/.env: NOTIFYHUB_DOMAIN, and POSTGRES_PASSWORD (e.g. `openssl rand -base64 36`)
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build
docker compose -f deploy/docker-compose.yml --env-file deploy/.env ps
curl https://notifyhub.example.com/health/ready
```

Expected: `migrate` exited 0; `postgres`, `api`, `worker` healthy; readiness returns
`{"status":"Healthy", ...}` with `database`, `schema` and `worker` checks.

Building behind a TLS-intercepting proxy: pass its CA as a build secret,
`docker build --secret id=extra_ca,src=/path/ca.crt ...` (it is not copied into the final images).

## Applying a new version (interim)

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env build
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d   # migrate runs before api/worker restart
```

A documented, tested upgrade/rollback procedure (including pre-migration backup) is a Phase 5 deliverable.

## Known gaps (tracked)

- Database runs as a single owner role; Phase 2/5 introduces separate migration and runtime roles (needed for RLS).
- No automated backups yet (Phase 5).
- Forwarded-header handling in the API is not configured; nothing in Phase 1 depends on the external scheme/host.
  Required before cookie authentication (Phase 4).
- Base images are pinned by tag, not digest.
