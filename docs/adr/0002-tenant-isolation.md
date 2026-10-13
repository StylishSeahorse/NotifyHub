# ADR 0002 — Tenant isolation: composite foreign keys now, query filters and RLS in Phase 2

- Status: Accepted (Phase 1 portion implemented)
- Date: 2026-10-13

## Context

Tenant isolation is a security boundary. Application bugs (a missing `WHERE tenant_id = …`) must not be able to create or
expose cross-tenant data.

## Decision

Three layers of defence:

1. **Schema (implemented in Phase 1).** Every tenant-owned table has `tenant_id NOT NULL`. Every tenant-owned table that is
   referenced by another tenant-owned table exposes an alternate key `(tenant_id, id)`, and every reference uses a
   **composite foreign key `(tenant_id, x_id) → x(tenant_id, id)`**. A row in tenant A therefore cannot reference a device,
   group, recipient, notification or asset in tenant B — PostgreSQL rejects it. Integration tests insert such rows directly
   with SQL (bypassing the domain) to prove this for every relationship.
2. **Application (Phase 2).** Tenant context is derived only from the authenticated principal's membership (never from a
   client-supplied tenant ID). An EF Core global query filter on `ITenantOwned` plus explicit authorisation checks scope
   every read and write.
3. **Row-level security (Phase 2).** Enable RLS on tenant-owned tables with a policy on `current_setting('notifyhub.tenant_id')`,
   set per transaction by a connection interceptor. The application connects as a non-owner role so policies apply; the
   migration role owns the schema. Platform-level jobs (scheduler, retention) use a dedicated role with `BYPASSRLS`, kept
   to the worker process.

RLS was deliberately not enabled in Phase 1: without the tenant-context plumbing it would either block all access or be
bypassed by the owner role, giving false assurance.

## Consequences

- Composite keys add a `tenant_id` column to some indexes and foreign keys; this is cheap and also helps tenant-scoped queries.
- `users` (portal principals) are global and are linked to tenants only via `tenant_memberships`; foreign keys from
  tenant-owned rows to `users` (e.g. `notifications.created_by`) are simple keys, so membership must be checked in the
  application layer when assigning such actors.
- `device_key_id` is **globally** unique (stronger than per-tenant), because device authentication resolves the tenant from
  the credential.
