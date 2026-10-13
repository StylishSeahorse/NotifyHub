# Threat model (initial — Phase 1)

Method: STRIDE over the main trust boundaries. Each threat lists its mitigation and **current status**. This document is
revised at every phase gate.

## Assets

Notification content; per-recipient dismissal/acknowledgement records (may have compliance weight); audit history;
device credentials; administrator sessions; tenant branding assets; tenant separation itself.

## Trust boundaries

1. Internet ↔ Caddy (TLS).
2. Caddy ↔ API (private Docker network).
3. API/Worker ↔ PostgreSQL (internal network, no internet route).
4. Windows service ↔ backend (device credential over HTTPS) — Phase 3.
5. Agent (user session) ↔ service (named pipe, local privilege boundary) — Phase 3.
6. Tenant A ↔ tenant B (logical boundary inside one database).

## Threats

| # | Threat | Mitigation | Status |
|---|---|---|---|
| T1 | Cross-tenant read/write via application bug (missing filter, forged tenant ID) | Composite FKs make cross-tenant references impossible; tenant derived from membership only; global query filters; RLS | FKs **implemented + tested**; filters/RLS Phase 2 |
| T2 | Cross-tenant targeting (notification in A targets device/group/recipient in B) | Typed target columns with composite FKs | **Implemented + tested** |
| T3 | User A's dismissal/acknowledgement affects user B on a shared PC | State keyed by `(tenant, notification, recipient)`; domain never touches other rows | **Implemented + tested** (domain and persistence) |
| T4 | Closing a critical popup recorded as acknowledgement | Separate close (`last_closed_at`) vs. explicit acknowledgement | **Implemented + tested** (domain) |
| T5 | Replay / duplicate event uploads cause repeated transitions | Unique `(tenant, device, idempotency_key)`; idempotent domain transitions | Constraint **implemented + tested**; ingestion endpoint Phase 2 |
| T6 | Back-dated or future-dated client timestamps (e.g. acknowledge "before" withdrawal) | Clamp to receipt time; accept ack only if notice was active at clamped time; keep raw time for forensics | **Implemented + tested** (domain). Residual: a device can back-date within the active window |
| T7 | Tampering with audit or event history | Append-only triggers reject UPDATE; actor/tenant/correlation recorded | Triggers **implemented + tested**; audit writing Phase 2 |
| T8 | Device credential theft / reuse on another machine | Per-device credential, globally unique key ID, revocation, rotation, OS-protected storage | Schema/domain states **implemented**; credentials Phase 3 |
| T9 | Device credential used against admin APIs | Separate authentication schemes and policies; admin policies reject device principals | Phase 3/4 (deny-by-default already in place) |
| T10 | Forged user identity from a device (arbitrary SID) | Service derives SID from pipe client token; server never accepts bare recipient IDs; residual local-admin risk documented | Design in ADR 0003; Phase 3 |
| T11 | Cross-session impersonation over named pipe | Per-session pipe ACLs; verify client PID/session/SID per connection; size limits; no TCP listener | Phase 3 |
| T12 | Stored XSS / code execution via notification content or branding | Content is plain text, rendered inert (WPF TextBlock, React text nodes); control characters rejected; theme is data-only JSON | Domain validation **implemented**; renderers Phase 3/4 |
| T13 | Malicious uploads / path traversal | MIME allowlist (PNG/JPEG/WebP; no SVG), size limit, SHA-256, server-generated storage keys; magic-byte sniffing | Domain + DB CHECKs **implemented**; upload pipeline Phase 4 |
| T14 | Information disclosure via errors/health | Problem details without stack traces; health returns statuses only | **Implemented + tested** |
| T15 | Log injection via correlation IDs | Inbound IDs validated (≤64 chars, `[A-Za-z0-9._-]`) else replaced | **Implemented + tested** |
| T16 | Unauthenticated access to future endpoints by omission | Fallback policy requires authentication for all requests; fail-closed default scheme | **Implemented + tested** |
| T17 | Database exposed to network | Internal Docker network, no published port, SCRAM auth | **Implemented + validated** in Compose |
| T18 | Reminder storm / denial of service on users | Bounded interval (≥5 min) and daily cap (≤48) | Bounds **implemented + tested**; scheduler enforcement Phase 2 |
| T19 | Oversized requests | Kestrel 1 MB default body limit; Caddy 2 MB body and 32 KB header limits | **Implemented** (Caddy config validated) |
| T20 | Rate abuse of enrolment/event endpoints | ASP.NET Core rate limiting per device / per IP | Phase 2/3 |
| T21 | Malicious update / unsigned binaries | Authenticode-signed binaries and MSI; update integrity verification | Phase 5 |
| T22 | Secrets in repository or images | No credentials committed; `.env` git-ignored; build-time CA via BuildKit secret only | **Implemented** |

## Accepted / residual risks (to revisit)

- Single database owner role in Phase 1 (no RLS yet). Mitigated by composite FKs; resolved in Phase 2.
- Offline devices cannot learn of a withdrawal until they reconnect; bounded by expiry and polling (Phase 3).
- An acknowledgement proves an authenticated action at a device, not comprehension.
