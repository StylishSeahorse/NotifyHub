# ADR 0004 — Notification lifecycle, revisions and recipient state

- Status: Accepted (domain rules implemented and unit-tested in Phase 1; workflows in Phase 2)
- Date: 2026-10-13

## Global lifecycle

```
Draft ──publish now──────────────▶ Published ──withdraw──▶ Withdrawn
  │  ▲                                 │
  │  └─unschedule─┐                    └──expiry passes──▶ Expired
  └─schedule────▶ Scheduled ──start time passes──▶ Published
                     │   └──(scheduler late, expiry passed)──▶ Expired
                     └──withdraw (cancel)──▶ Withdrawn
```

- Only drafts are editable. Published content is immutable.
- **Revisions:** a material change to a scheduled/published notice is a new row with the same `series_id`,
  `revision + 1`, `supersedes_notification_id = previous`. It starts as a draft (targets and expiry copied). When it
  becomes active, the previous revision is withdrawn and gets `superseded_by_notification_id`, in the same transaction.
  Each revision has its own recipient state: acknowledging revision 1 does not acknowledge revision 2, and
  `acknowledged_revision` records exactly what was acknowledged.
- Optimistic concurrency uses PostgreSQL `xmin`.

## Recipient state (per tenant, notification, recipient)

- Normal notices: dismissal sets `dismissed_at` (first one wins) and hides the notice for that recipient across logons
  and devices. Nothing else changes.
- Notices requiring acknowledgement: closing sets `last_closed_at` and `next_reminder_at = closed + interval`; it never
  suppresses. Acknowledgement sets `acknowledged_at`, `acknowledged_revision`, `acknowledged_on_device_id` and clears
  `next_reminder_at`. Repeat acknowledgements are no-ops.
- An acknowledgement is accepted only if the notice **was active when it occurred** (`WasActiveAt`). Offline
  acknowledgements made before a withdrawal are preserved; ones made after it are rejected.
- Client timestamps are untrusted: they are clamped to the server receipt time. The reported time is kept on the event
  row for forensics.
- Recipient state never modifies global status.

## Reminder policy

`interval` 5 minutes–7 days (default 60), `max presentations/day` 1–48 (default 8), `re-present at logon` (default true).
Stored as `jsonb`. Quiet hours, escalation and the per-day cap enforcement require tenant time-zone settings and are
Phase 2 work.

## Events and idempotency

`notification_events` is append-only (database trigger) and unique on `(tenant_id, device_id, idempotency_key)`; scoping by
device prevents one device from pre-empting another's keys.
