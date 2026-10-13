# NotifyHub

Self-hosted, multi-tenant platform for publishing branded notifications to shared Windows computers, with strictly
per-user dismissal and acknowledgement. ("NotifyHub" is a provisional name.)

> **Status: Phase 1 complete (foundation).** Domain model, PostgreSQL schema and migrations, API and worker hosts with
> health checks, Docker Compose deployment and automated tests exist. There are no business API endpoints, no Windows
> components and no admin portal yet. Not production-ready. See [`docs/phase-1-report.md`](docs/phase-1-report.md).

## Repository layout

```
src/
  NotifyHub.Domain/          entities, lifecycle state machine, recipient-state rules (no dependencies)
  NotifyHub.Contracts/       versioned wire contracts (shared with Windows components)
  NotifyHub.Application/     use cases and ports
  NotifyHub.Infrastructure/  EF Core + PostgreSQL, migrations, health checks
  NotifyHub.Api/             ASP.NET Core API host (+ `migrate`, `healthcheck` commands)
  NotifyHub.Worker/          background worker host
tests/
  NotifyHub.UnitTests/         domain rules
  NotifyHub.IntegrationTests/  real PostgreSQL (Testcontainers): migrations, constraints, API pipeline, worker
deploy/
  docker-compose.yml, docker/Dockerfile, reverse-proxy/Caddyfile, .env.example
docs/
  architecture.md, adr/, threat-model.md, api-contracts.md, development.md, deployment.md, operations.md
```

Planned: `src/NotifyHub.Service`, `src/NotifyHub.Agent`, `web/notifyhub-admin`, `installer/`, Windows and security
test projects.

## Quick start

```bash
dotnet tool restore
dotnet build NotifyHub.slnx
dotnet test NotifyHub.slnx          # integration tests need Docker
```

Run the full stack:

```bash
cp deploy/.env.example deploy/.env   # set NOTIFYHUB_DOMAIN and POSTGRES_PASSWORD
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build
curl https://<your-domain>/health/ready
```

## Documentation

- [Architecture](docs/architecture.md) and [ADRs](docs/adr/)
- [Threat model](docs/threat-model.md)
- [API contracts](docs/api-contracts.md)
- [Development](docs/development.md) · [Deployment](docs/deployment.md) · [Operations](docs/operations.md)

## Technology

.NET 10 LTS (C#, ASP.NET Core, EF Core 10, Npgsql), PostgreSQL 17, Docker Compose, Caddy. Planned: SignalR, React +
TypeScript, WPF, Windows toast notifications, named pipes, SQLite, signed MSI.
