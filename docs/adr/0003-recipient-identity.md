# ADR 0003 — Recipients: tenant-scoped Windows identities, separate from portal users

- Status: Accepted; enrolment/verification mechanics to be validated in Phase 3
- Date: 2026-10-13

## Context

Per-user dismissal and acknowledgement on shared computers is the core requirement. A machine credential proves *which
device* is talking, not *which person* is at it. Usernames and display names are not stable identities.

The specification lists a single `users` table referenced by `notification_user_state.user_id`. Portal administrators and
Windows session users are, however, different populations with different identity sources and lifecycles, and a global
`users` table cannot be tied to a tenant with a composite foreign key.

## Decision

- `users` = portal principals (administrators, publishers, auditors), keyed by `(identity_provider, external_subject)`.
  Email is stored but is never an identity key. Access to tenants is via `tenant_memberships`.
- `recipients` = **tenant-scoped** notification recipients, keyed by `(tenant_id, kind, authority, subject)`:

  | kind              | authority                  | subject                  |
  |-------------------|----------------------------|--------------------------|
  | `ActiveDirectory` | domain SID                 | user SID (must be in domain) |
  | `EntraId`         | Entra tenant ID (GUID)     | user object ID (GUID)    |
  | `LocalAccount`    | device ID                  | local account SID; `local_device_id` FK to the device |

- Per-user state lives in `notification_recipient_states (tenant_id, notification_id, recipient_id)` with composite FKs.
  Deliveries and events reference `recipient_id` the same way.
- The same AD/Entra person in two tenants is two independent recipient rows (proven by a test).

## Trust model (to be implemented and validated in Phase 3)

- The server never accepts a bare recipient identifier from a device. The Windows **service** (not the agent) determines
  the SID of the interactive session from the named-pipe client token (`GetNamedPipeClientProcessId` → process token →
  user SID, and session ID cross-check) and reports `(session, SID)` to the backend under the device credential.
- The backend maps `(device's tenant, SID/domain SID)` → recipient. For Entra-joined devices the service resolves the
  Entra object ID from the logged-on user's token / `dsregcmd` data.
- Residual risk: a device credential holder with local administrator rights can assert arbitrary SIDs for that device.
  This is inherent in device-asserted identity; it is limited to that device and tenant, recorded in the event stream, and
  documented in the threat model. Where stronger assurance is required, an optional per-user authentication step (e.g. an
  Entra token obtained by the agent and verified by the backend) will be offered — decided in Phase 3.
- Local accounts are device-scoped: the same local SID on two machines is two recipients.

## Consequences

- Column names differ from the specification (`recipient_id` instead of `user_id`). The API will still present "users"
  in the portal where that is clearer to administrators.
- User-targeted notifications reference `recipients`, so administrators can only target people who have a recipient record
  (seen on an enrolled device, or created ahead of time from directory data — Phase 4).
