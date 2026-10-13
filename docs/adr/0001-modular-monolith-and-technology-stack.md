# ADR 0001 — Modular monolith on .NET 10 LTS, PostgreSQL and Docker Compose

- Status: Accepted (Phase 1)
- Date: 2026-10-13

## Context

NotifyHub must be self-hosted by organisations of modest operational maturity. The specification mandates C#/ASP.NET Core,
PostgreSQL, React, WPF, named pipes and Docker Compose, and asks that unnecessary infrastructure (Kubernetes, Redis,
message brokers, microservices) be avoided in the MVP.

## Decision

- One backend deployable split into two processes from the same codebase: `NotifyHub.Api` (HTTP, later SignalR) and
  `NotifyHub.Worker` (scheduling, expiry, reminders, retention). Both use `NotifyHub.Infrastructure`.
- Layering, enforced by project references:
  `Domain` ← `Application` ← `Infrastructure` ← `Api` / `Worker`. `Contracts` holds versioned wire DTOs and depends on
  nothing; `Domain` depends on nothing (no EF Core, ASP.NET or UI types).
- .NET 10 (LTS, supported to November 2028). The SDK is pinned in `global.json` (`10.0.100`, roll forward within 10.0
  feature bands). NuGet versions are pinned centrally in `Directory.Packages.props` with transitive pinning. `dotnet-ef` is
  a pinned local tool.
- PostgreSQL 17 (`postgres:17.11-alpine`), EF Core 10 with Npgsql, snake_case naming.
- Docker Compose with Caddy as the TLS-terminating reverse proxy.
- The Windows service and agent (`NotifyHub.Service`, `NotifyHub.Agent`) and the React portal will be added in their
  phases (3 and 4). No placeholder projects were created for them.

## Consequences

- Scaling is vertical plus "more worker/API replicas against one database". The worker's jobs must therefore be safe to run
  concurrently (row locking / `SKIP LOCKED`), which Phase 2 must honour.
- A single database is the integration point; there is no broker. Real-time fan-out to devices will use SignalR with
  polling fallback; if multiple API replicas are run, a SignalR backplane decision will be needed (deferred until measured).
