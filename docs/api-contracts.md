# API contracts

The machine-readable contract is the OpenAPI document served at `/openapi/v1.json` (generated from the running code, so
it cannot drift from the implementation). This page tracks implementation status of the planned `/api/v1` surface.

## Conventions (in force)

- Base path `/api/v1`. Breaking changes require `/api/v2`.
- Errors: RFC 9457 `application/problem+json` with `status`, `title`, `detail`, `correlationId` and, for business-rule
  violations, a stable `code` (HTTP 422).
- Every response carries `X-Correlation-ID`; a valid inbound value is echoed.
- All endpoints require authentication unless listed as anonymous below.

## Implemented (Phase 1)

| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | `/health/live` | anonymous | `{"status":"Healthy","checks":{}}` |
| GET | `/health/ready` | anonymous | `{"status":…, "checks":{"database":…, "schema":…, "worker":…}}`, 503 when Unhealthy |
| GET | `/openapi/v1.json` | anonymous | Disable with `Api:EnableOpenApi=false` |

Any other request currently returns **401** problem details (deny by default).

## Planned

| Method | Path | Principal | Phase |
|---|---|---|---|
| GET/POST | `/api/v1/tenants` | Platform administrator | 2 |
| GET/PATCH | `/api/v1/tenants/{id}/branding` | Tenant administrator | 2/4 |
| GET/POST | `/api/v1/notifications` | Publisher / tenant administrator (GET: auditor) | 2 |
| GET/PATCH | `/api/v1/notifications/{id}` | as above | 2 |
| POST | `/api/v1/notifications/{id}/publish` | Publisher | 2 |
| POST | `/api/v1/notifications/{id}/withdraw` | Publisher | 2 |
| GET | `/api/v1/notifications/{id}/report` | Publisher / auditor | 2 |
| GET/POST | `/api/v1/devices` | Tenant administrator (GET: auditor) | 2/3 |
| GET/POST | `/api/v1/device-groups` | Tenant administrator | 2 |
| GET | `/api/v1/audit-events` | Tenant administrator / auditor | 2 |
| POST | `/api/v1/device-enrolments` | Enrolment token | 3 |
| POST | `/api/v1/device/check-in` | Device | 3 |
| GET | `/api/v1/device/notifications` | Device | 3 |
| POST | `/api/v1/device/events` | Device (idempotency key per event) | 2/3 |
| GET | `/api/v1/device/configuration` | Device | 3 |
| POST | `/api/v1/device/heartbeat` | Device | 3 |
| WS | SignalR hub for device/portal push | Device / admin | 3/4 |

Windows service ↔ agent named-pipe operations (`GetActiveNotifications`, `GetNotificationState`, `DismissNotification`,
`AcknowledgeNotification`, `ReportAgentHealth`, `SynchronisePendingEvents`) will be specified in `NotifyHub.Contracts`
in Phase 3.
