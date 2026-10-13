namespace NotifyHub.Infrastructure.Persistence.Sql;

/// <summary>
/// Database-level enforcement that audit and client event records are immutable once written. Rows may be
/// deleted only by retention cleanup; updates are always rejected, whichever code path attempts them.
/// </summary>
internal static class AppendOnlyTables
{
    public const string Up = """
        CREATE FUNCTION notifyhub_reject_update() RETURNS trigger
        LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION 'Table % is append-only; updates are not permitted', TG_TABLE_NAME
                USING ERRCODE = 'restrict_violation';
        END;
        $$;

        CREATE TRIGGER trg_audit_events_append_only
            BEFORE UPDATE ON audit_events
            FOR EACH ROW EXECUTE FUNCTION notifyhub_reject_update();

        CREATE TRIGGER trg_notification_events_append_only
            BEFORE UPDATE ON notification_events
            FOR EACH ROW EXECUTE FUNCTION notifyhub_reject_update();
        """;

    public const string Down = """
        DROP TRIGGER IF EXISTS trg_notification_events_append_only ON notification_events;
        DROP TRIGGER IF EXISTS trg_audit_events_append_only ON audit_events;
        DROP FUNCTION IF EXISTS notifyhub_reject_update();
        """;
}
