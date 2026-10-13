# Development guide

## Prerequisites

- .NET SDK 10.0.1xx (pinned in `global.json`).
- Docker (integration tests start PostgreSQL with Testcontainers), or an existing PostgreSQL 15+ server.
- The Windows components (Phase 3) will additionally require Windows 10/11 with the .NET desktop workload.

## Build and test

```bash
dotnet tool restore                      # pinned dotnet-ef
dotnet build NotifyHub.slnx              # warnings are errors
dotnet format NotifyHub.slnx --verify-no-changes
dotnet test tests/NotifyHub.UnitTests
dotnet test tests/NotifyHub.IntegrationTests   # needs Docker, or NOTIFYHUB_TEST_POSTGRES
```

Integration-test settings:

| Variable | Effect |
|---|---|
| `NOTIFYHUB_TEST_POSTGRES` | Use this server (connection string with CREATEDB) instead of a container |
| `NOTIFYHUB_TEST_POSTGRES_IMAGE` | Override the container image (default `postgres:17.11-alpine`) |
| `TESTCONTAINERS_RYUK_DISABLED=true` | Skip the Ryuk reaper container if your registry access is restricted |

Every integration-test class gets its own freshly migrated database, created by the production `DatabaseMigrator`.

## Running locally

```bash
export ConnectionStrings__NotifyHub="Host=localhost;Database=notifyhub;Username=notifyhub;Password=<local password>"
dotnet run --project src/NotifyHub.Api -- migrate     # apply migrations, then exit
dotnet run --project src/NotifyHub.Api                # http://localhost:5152/health/ready
dotnet run --project src/NotifyHub.Worker
```

No connection strings or secrets are committed. Use environment variables or `dotnet user-secrets`.

## Database migrations

```bash
dotnet ef migrations add <Name> -p src/NotifyHub.Infrastructure -s src/NotifyHub.Infrastructure -o Persistence/Migrations
dotnet ef migrations script --idempotent -p src/NotifyHub.Infrastructure -s src/NotifyHub.Infrastructure
```

Rules:

- Never edit a migration that has been merged; add a new one.
- Every migration must have a working `Down` (a test migrates to zero and back up).
- The test `Migrations_apply_to_an_empty_database_and_match_the_model` fails if the model has changes without a migration.
- Raw SQL (triggers, RLS policies) goes in `Persistence/Sql/*.cs` constants referenced from the migration.

## Conventions

- Nullable reference types, file-scoped namespaces, `_camelCase` private fields, PascalCase constants.
- Domain methods take `DateTimeOffset now` instead of reading a clock; hosts inject `TimeProvider`.
- Domain rule violations throw `DomainException` with a stable `code`; the API maps them to 422 problem details.
