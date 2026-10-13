# NotifyHub architecture

> Status: **Phase 1 (foundation)**. This document describes what exists and what is planned; planned items are marked.

## System overview

```
 Browsers (admin portal, Phase 4)          Windows computers
          │ HTTPS                       ┌──────────────────────────────┐
          ▼                             │ NotifyHub.Service (Phase 3)  │
   ┌─────────────┐   HTTPS / WSS        │  Worker Service, low-priv    │──┐ outbound HTTPS only
   │   Caddy     │◀────────────────────────────────────────────────────────┘
   └──────┬──────┘                      │      ▲ named pipe (ACL'd)    │
          │ HTTP (frontend network)     │      ▼                       │
   ┌──────▼──────┐                      │ NotifyHub.Agent ×N sessions  │
   │ NotifyHub   │                      │  WPF + toast (Phase 3)       │
   │ .Api        │                      └──────────────────────────────┘
   └──────┬──────┘
          │ backend network (internal: no internet route)
   ┌──────▼──────┐     ┌──────────────┐
   │ PostgreSQL  │◀────│ NotifyHub    │
   │ 17          │     │ .Worker      │
   └─────────────┘     └──────────────┘
```

## Projects and dependency rules

| Project | Responsibility | May reference |
|---|---|---|
| `NotifyHub.Domain` | Entities, invariants, lifecycle state machine, recipient-state rules | nothing |
| `NotifyHub.Contracts` | Versioned wire DTOs shared with Windows components | nothing |
| `NotifyHub.Application` | Use cases and ports (Phase 1: worker heartbeat port only) | Domain, Contracts |
| `NotifyHub.Infrastructure` | EF Core/PostgreSQL, migrations, health checks, port implementations | Application |
| `NotifyHub.Api` | HTTP pipeline, health endpoints, OpenAPI, `migrate` command | Infrastructure |
| `NotifyHub.Worker` | Hosted background services (Phase 1: heartbeat) | Infrastructure |
| `NotifyHub.Service` / `NotifyHub.Agent` | Windows service / per-session agent — **Phase 3** | Contracts only |
| `web/notifyhub-admin` | React + TypeScript portal — **Phase 4** | HTTP API only |

The Windows components will depend only on `Contracts`, never on domain or database types.

## Data model (implemented)

All primary keys are UUIDv7 (time-ordered), all timestamps `timestamptz` in UTC, enums stored as text with CHECK
constraints. See the initial migration in `src/NotifyHub.Infrastructure/Persistence/Migrations`.

| Table | Purpose | Key constraints |
|---|---|---|
| `tenants` | Organisations | unique `slug` + format CHECK |
| `tenant_branding` | Theme JSON, logo, `branding_version` | PK `tenant_id`; logo FK `(tenant_id, logo_asset_id)` |
| `users` | Portal principals | unique `(identity_provider, external_subject)`; email non-unique |
| `platform_administrators` | Platform-level role, separate from tenant roles | PK `user_id` |
| `tenant_memberships` | One role per user per tenant | PK `(tenant_id, user_id)` |
| `recipients` | Tenant-scoped Windows identities (ADR 0003) | unique `(tenant_id, kind, authority, subject)`; local accounts FK to device |
| `devices` | Enrolled computers | AK `(tenant_id, id)`; globally unique `device_key_id`; state CHECKs |
| `device_groups`, `device_group_members` | Grouping | unique `(tenant_id, normalized_name)`; composite FKs |
| `notifications` | Content + global lifecycle + revision lineage | unique `(tenant_id, series_id, revision)`; lifecycle CHECKs; `xmin` concurrency |
| `notification_targets` | Requested audience | typed nullable FK columns (each composite) + CHECK exactly-one; unique per target |
| `notification_recipient_states` | Per-person dismissal / acknowledgement | PK `(tenant_id, notification_id, recipient_id)`; partial index on unacknowledged |
| `notification_deliveries` | Server-side send tracking | unique `(tenant, notification, device, recipient)` NULLS NOT DISTINCT |
| `notification_events` | Client-reported events, append-only | unique `(tenant_id, device_id, idempotency_key)`; UPDATE trigger rejects changes |
| `assets` | Uploaded images metadata | server-generated `storage_key`; MIME/size/hash CHECKs |
| `audit_events` | Append-only audit trail | UPDATE trigger rejects changes |
| `worker_heartbeats` | Worker liveness | PK `(worker_name, instance_id)` |

Indexes exist for due schedules (`status='Scheduled'`), due expiries (`status='Published' AND expires_at IS NOT NULL`),
active notices per tenant, unresolved acknowledgements, recipient history and device health.

### Deviations from the specification's logical schema (intentional)

- `notification_user_state.user_id` → `notification_recipient_states.recipient_id` (ADR 0003).
- `notification_targets.target_id` (polymorphic) → `device_group_id` / `device_id` / `recipient_id`, so that each has a
  real tenant-consistent foreign key.
- Additional columns for revision lineage, actors and reminder state (ADR 0004); `devices.created_at` and a `Pending`
  status for pre-registered devices.
- Deferred tables: device credentials and enrolment tokens (Phase 3), refresh tokens/sessions (Phase 4), reminder schedule
  materialisation if required by measured load (Phase 2).

## Tenant isolation

See ADR 0002. Phase 1 implements and tests the database layer (composite foreign keys). Application-level scoping and
PostgreSQL RLS are Phase 2.

## Notification semantics

See ADR 0004. The domain enforces: drafts only are editable; publication requires targets and an unexpired window;
scheduled notices that the scheduler reaches after expiry go straight to Expired; a revision withdraws its predecessor
when it becomes active; dismissal and acknowledgement are per recipient and never touch global status; closing a notice
that requires acknowledgement defers but never suppresses it; acknowledgements are accepted only for the period when the
notice was active.

## Windows architecture (Phase 3 plan)

- `NotifyHub.Service`: .NET Worker Service running as a dedicated virtual account (`NT SERVICE\NotifyHub`), not
  LocalSystem. Owns the device credential (DPAPI machine scope / CNG key), SQLite cache, sync, session tracking
  (`WTSRegisterSessionNotification`, `WTSEnumerateSessions`), and the named-pipe server.
- `NotifyHub.Agent`: per-session WPF process started at logon (HKLM Run key or scheduled task "at logon of any user"),
  running as the user, unelevated. Per-session single-instance using a `Local\` (session-scoped) mutex, never `Global\`.
- IPC: one pipe instance per session; ACL grants the service full control and the interactive user read/write; the service
  verifies the client's process ID, session ID and token SID on every connection; length-prefixed, size-capped,
  schema-validated messages; only the documented operations (`GetActiveNotifications`, `GetNotificationState`,
  `DismissNotification`, `AcknowledgeNotification`, `ReportAgentHealth`, `SynchronisePendingEvents`).
- This is the highest-risk area and must be validated on real Windows 10/11 and Windows Server (RDS) machines.

## Unresolved decisions

| Topic | Options | Decide in |
|---|---|---|
| Device credential type | Client certificate (mTLS at Caddy) vs. per-device asymmetric key with signed requests | Phase 3 |
| Per-user assurance beyond device-asserted SID | Optional Entra token verification | Phase 3 |
| Service account | Virtual account vs. LocalService — depends on WTS/session APIs needed | Phase 3 |
| Admin identity | OIDC (preferred) vs. ASP.NET Core Identity local accounts with MFA | Phase 4 |
| Multiple API replicas | SignalR backplane (PostgreSQL LISTEN/NOTIFY vs. Redis) | When measured |
| Tenant time zones | Required for quiet hours / per-day reminder caps | Phase 2 |
