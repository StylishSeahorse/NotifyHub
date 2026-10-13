using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NotifyHub.Infrastructure.Persistence.Sql;

#nullable disable

namespace NotifyHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                    table.CheckConstraint("ck_tenants_slug_format", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$' AND char_length(slug) BETWEEN 3 AND 63");
                    table.CheckConstraint("ck_tenants_status", "status IN ('Active', 'Suspended', 'Deactivated')");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    external_subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_status", "status IN ('Active', 'Disabled')");
                });

            migrationBuilder.CreateTable(
                name: "worker_heartbeats",
                columns: table => new
                {
                    worker_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_heartbeat_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_worker_heartbeats", x => new { x.worker_name, x.instance_id });
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_events", x => x.id);
                    table.CheckConstraint("ck_audit_events_actor", "actor_type = 'System' OR actor_id IS NOT NULL");
                    table.CheckConstraint("ck_audit_events_actor_type", "actor_type IN ('User', 'Device', 'Recipient', 'System')");
                    table.ForeignKey(
                        name: "fk_audit_events_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "device_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_groups", x => x.id);
                    table.UniqueConstraint("ak_device_groups_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_device_groups_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    device_key_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    operating_system = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    agent_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    enrolled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_devices", x => x.id);
                    table.UniqueConstraint("ak_devices_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_devices_active_has_key", "status NOT IN ('Active', 'Quarantined') OR device_key_id IS NOT NULL");
                    table.CheckConstraint("ck_devices_revoked_at", "status <> 'Revoked' OR revoked_at IS NOT NULL");
                    table.CheckConstraint("ck_devices_status", "status IN ('Pending', 'Active', 'Quarantined', 'Revoked')");
                    table.ForeignKey(
                        name: "fk_devices_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assets", x => x.id);
                    table.UniqueConstraint("ak_assets_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_assets_mime_type", "mime_type IN ('image/jpeg', 'image/png', 'image/webp')");
                    table.CheckConstraint("ck_assets_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_assets_size", "size_bytes > 0 AND size_bytes <= 2097152");
                    table.ForeignKey(
                        name: "fk_assets_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assets_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    series_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    supersedes_notification_id = table.Column<Guid>(type: "uuid", nullable: true),
                    superseded_by_notification_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    display_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    requires_acknowledgement = table.Column<bool>(type: "boolean", nullable: false),
                    reminder_policy = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_by = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    withdrawn_by = table.Column<Guid>(type: "uuid", nullable: true),
                    withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.UniqueConstraint("ak_notifications_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_notifications_display_mode", "display_mode IN ('Toast', 'Popup')");
                    table.CheckConstraint("ck_notifications_expired_at", "status <> 'Expired' OR expired_at IS NOT NULL");
                    table.CheckConstraint("ck_notifications_published_at", "status NOT IN ('Published') OR published_at IS NOT NULL");
                    table.CheckConstraint("ck_notifications_revision_lineage", "(revision = 1) = (supersedes_notification_id IS NULL)");
                    table.CheckConstraint("ck_notifications_revision_positive", "revision >= 1");
                    table.CheckConstraint("ck_notifications_scheduled_has_start", "status <> 'Scheduled' OR starts_at IS NOT NULL");
                    table.CheckConstraint("ck_notifications_severity", "severity IN ('Info', 'Warning', 'Critical')");
                    table.CheckConstraint("ck_notifications_status", "status IN ('Draft', 'Scheduled', 'Published', 'Withdrawn', 'Expired')");
                    table.CheckConstraint("ck_notifications_window", "expires_at IS NULL OR starts_at IS NULL OR expires_at > starts_at");
                    table.CheckConstraint("ck_notifications_withdrawn_at", "status <> 'Withdrawn' OR withdrawn_at IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_notifications_superseded_by",
                        columns: x => new { x.tenant_id, x.superseded_by_notification_id },
                        principalTable: "notifications",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notifications_supersedes",
                        columns: x => new { x.tenant_id, x.supersedes_notification_id },
                        principalTable: "notifications",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notifications_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notifications_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notifications_users_published_by",
                        column: x => x.published_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notifications_users_withdrawn_by",
                        column: x => x.withdrawn_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "platform_administrators",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_administrators", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_platform_administrators_users_granted_by",
                        column: x => x.granted_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_platform_administrators_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_memberships",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_memberships", x => new { x.tenant_id, x.user_id });
                    table.CheckConstraint("ck_tenant_memberships_role", "role IN ('TenantAdministrator', 'Publisher', 'Auditor')");
                    table.CheckConstraint("ck_tenant_memberships_status", "status IN ('Active', 'Suspended')");
                    table.ForeignKey(
                        name: "fk_tenant_memberships_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_memberships_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "device_group_members",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_group_members", x => new { x.tenant_id, x.device_group_id, x.device_id });
                    table.ForeignKey(
                        name: "fk_device_group_members_device",
                        columns: x => new { x.tenant_id, x.device_id },
                        principalTable: "devices",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_device_group_members_group",
                        columns: x => new { x.tenant_id, x.device_group_id },
                        principalTable: "device_groups",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    authority = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    local_device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipients", x => x.id);
                    table.UniqueConstraint("ak_recipients_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_recipients_kind", "kind IN ('ActiveDirectory', 'EntraId', 'LocalAccount')");
                    table.CheckConstraint("ck_recipients_local_device", "(kind = 'LocalAccount') = (local_device_id IS NOT NULL)");
                    table.CheckConstraint("ck_recipients_status", "status IN ('Active', 'Disabled')");
                    table.ForeignKey(
                        name: "fk_recipients_local_device",
                        columns: x => new { x.tenant_id, x.local_device_id },
                        principalTable: "devices",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recipients_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_branding",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    logo_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    theme_json = table.Column<string>(type: "jsonb", nullable: false),
                    branding_version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_branding", x => x.tenant_id);
                    table.CheckConstraint("ck_tenant_branding_version_positive", "branding_version >= 1");
                    table.ForeignKey(
                        name: "fk_tenant_branding_logo_asset",
                        columns: x => new { x.tenant_id, x.logo_asset_id },
                        principalTable: "assets",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_branding_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    delivery_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    first_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_deliveries", x => x.id);
                    table.CheckConstraint("ck_notification_deliveries_attempts", "attempt_count >= 1");
                    table.CheckConstraint("ck_notification_deliveries_delivery_status", "delivery_status IN ('Pending', 'Sent', 'Delivered', 'Failed')");
                    table.ForeignKey(
                        name: "fk_notification_deliveries_device",
                        columns: x => new { x.tenant_id, x.device_id },
                        principalTable: "devices",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notification_deliveries_notification",
                        columns: x => new { x.tenant_id, x.notification_id },
                        principalTable: "notifications",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notification_deliveries_recipient",
                        columns: x => new { x.tenant_id, x.recipient_id },
                        principalTable: "recipients",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    event_metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_events", x => x.id);
                    table.CheckConstraint("ck_notification_events_event_type", "event_type IN ('DeliveryAttempted', 'Delivered', 'Displayed', 'DisplayFailed', 'Dismissed', 'Acknowledged')");
                    table.ForeignKey(
                        name: "fk_notification_events_device",
                        columns: x => new { x.tenant_id, x.device_id },
                        principalTable: "devices",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notification_events_notification",
                        columns: x => new { x.tenant_id, x.notification_id },
                        principalTable: "notifications",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notification_events_recipient",
                        columns: x => new { x.tenant_id, x.recipient_id },
                        principalTable: "recipients",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_recipient_states",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dismissed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    acknowledged_revision = table.Column<int>(type: "integer", nullable: true),
                    acknowledged_on_device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    first_presented_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_presented_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    presentation_count = table.Column<int>(type: "integer", nullable: false),
                    next_reminder_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_recipient_states", x => new { x.tenant_id, x.notification_id, x.recipient_id });
                    table.CheckConstraint("ck_notification_recipient_states_ack", "(acknowledged_at IS NULL) = (acknowledged_revision IS NULL) AND (acknowledged_at IS NULL) = (acknowledged_on_device_id IS NULL)");
                    table.CheckConstraint("ck_notification_recipient_states_presentations", "presentation_count >= 0");
                    table.ForeignKey(
                        name: "fk_notification_recipient_states_ack_device",
                        columns: x => new { x.tenant_id, x.acknowledged_on_device_id },
                        principalTable: "devices",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notification_recipient_states_notification",
                        columns: x => new { x.tenant_id, x.notification_id },
                        principalTable: "notifications",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notification_recipient_states_recipient",
                        columns: x => new { x.tenant_id, x.recipient_id },
                        principalTable: "recipients",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_targets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    device_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_targets", x => x.id);
                    table.CheckConstraint("ck_notification_targets_reference", "(target_type = 'AllDevices' AND device_group_id IS NULL AND device_id IS NULL AND recipient_id IS NULL)\nOR (target_type = 'DeviceGroup' AND device_group_id IS NOT NULL AND device_id IS NULL AND recipient_id IS NULL)\nOR (target_type = 'Device' AND device_group_id IS NULL AND device_id IS NOT NULL AND recipient_id IS NULL)\nOR (target_type = 'Recipient' AND device_group_id IS NULL AND device_id IS NULL AND recipient_id IS NOT NULL)");
                    table.CheckConstraint("ck_notification_targets_target_type", "target_type IN ('AllDevices', 'DeviceGroup', 'Device', 'Recipient')");
                    table.ForeignKey(
                        name: "fk_notification_targets_device",
                        columns: x => new { x.tenant_id, x.device_id },
                        principalTable: "devices",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notification_targets_device_group",
                        columns: x => new { x.tenant_id, x.device_group_id },
                        principalTable: "device_groups",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notification_targets_notification",
                        columns: x => new { x.tenant_id, x.notification_id },
                        principalTable: "notifications",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notification_targets_recipient",
                        columns: x => new { x.tenant_id, x.recipient_id },
                        principalTable: "recipients",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assets_created_by",
                table: "assets",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_assets_storage_key",
                table: "assets",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_entity_type_entity_id",
                table: "audit_events",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_tenant_id_occurred_at",
                table: "audit_events",
                columns: new[] { "tenant_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_device_group_members_tenant_id_device_id",
                table: "device_group_members",
                columns: new[] { "tenant_id", "device_id" });

            migrationBuilder.CreateIndex(
                name: "ix_device_groups_tenant_id_normalized_name",
                table: "device_groups",
                columns: new[] { "tenant_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_devices_device_key_id",
                table: "devices",
                column: "device_key_id",
                unique: true,
                filter: "device_key_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_devices_tenant_id_status_last_seen_at",
                table: "devices",
                columns: new[] { "tenant_id", "status", "last_seen_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_tenant_id_device_id",
                table: "notification_deliveries",
                columns: new[] { "tenant_id", "device_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_tenant_id_recipient_id",
                table: "notification_deliveries",
                columns: new[] { "tenant_id", "recipient_id" });

            migrationBuilder.CreateIndex(
                name: "ux_notification_deliveries_target",
                table: "notification_deliveries",
                columns: new[] { "tenant_id", "notification_id", "device_id", "recipient_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_notification_events_tenant_id_device_id_idempotency_key",
                table: "notification_events",
                columns: new[] { "tenant_id", "device_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_events_tenant_id_notification_id_occurred_at",
                table: "notification_events",
                columns: new[] { "tenant_id", "notification_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_events_tenant_id_recipient_id_occurred_at",
                table: "notification_events",
                columns: new[] { "tenant_id", "recipient_id", "occurred_at" },
                filter: "recipient_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notification_recipient_states_tenant_id_acknowledged_on_dev",
                table: "notification_recipient_states",
                columns: new[] { "tenant_id", "acknowledged_on_device_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_recipient_states_tenant_id_recipient_id",
                table: "notification_recipient_states",
                columns: new[] { "tenant_id", "recipient_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_recipient_states_unacknowledged",
                table: "notification_recipient_states",
                columns: new[] { "tenant_id", "notification_id" },
                filter: "acknowledged_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notification_targets_tenant_id_device_group_id",
                table: "notification_targets",
                columns: new[] { "tenant_id", "device_group_id" },
                filter: "device_group_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notification_targets_tenant_id_device_id",
                table: "notification_targets",
                columns: new[] { "tenant_id", "device_id" },
                filter: "device_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notification_targets_tenant_id_notification_id",
                table: "notification_targets",
                columns: new[] { "tenant_id", "notification_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_targets_tenant_id_recipient_id",
                table: "notification_targets",
                columns: new[] { "tenant_id", "recipient_id" },
                filter: "recipient_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_notification_targets_unique_target",
                table: "notification_targets",
                columns: new[] { "notification_id", "target_type", "device_group_id", "device_id", "recipient_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_active",
                table: "notifications",
                columns: new[] { "tenant_id", "published_at" },
                filter: "status = 'Published'");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_created_by",
                table: "notifications",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_due_expiry",
                table: "notifications",
                column: "expires_at",
                filter: "status = 'Published' AND expires_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_due_schedule",
                table: "notifications",
                column: "starts_at",
                filter: "status = 'Scheduled'");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_published_by",
                table: "notifications",
                column: "published_by");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_tenant_id_series_id_revision",
                table: "notifications",
                columns: new[] { "tenant_id", "series_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_tenant_id_status_created_at",
                table: "notifications",
                columns: new[] { "tenant_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_tenant_id_superseded_by_notification_id",
                table: "notifications",
                columns: new[] { "tenant_id", "superseded_by_notification_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_tenant_id_supersedes_notification_id",
                table: "notifications",
                columns: new[] { "tenant_id", "supersedes_notification_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_withdrawn_by",
                table: "notifications",
                column: "withdrawn_by");

            migrationBuilder.CreateIndex(
                name: "ix_platform_administrators_granted_by",
                table: "platform_administrators",
                column: "granted_by");

            migrationBuilder.CreateIndex(
                name: "ix_recipients_tenant_id_kind_authority_subject",
                table: "recipients",
                columns: new[] { "tenant_id", "kind", "authority", "subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recipients_tenant_id_local_device_id",
                table: "recipients",
                columns: new[] { "tenant_id", "local_device_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_branding_tenant_id_logo_asset_id",
                table: "tenant_branding",
                columns: new[] { "tenant_id", "logo_asset_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_memberships_user_id",
                table: "tenant_memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_slug",
                table: "tenants",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_users_identity_provider_external_subject",
                table: "users",
                columns: new[] { "identity_provider", "external_subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_worker_heartbeats_worker_name_last_heartbeat_at",
                table: "worker_heartbeats",
                columns: new[] { "worker_name", "last_heartbeat_at" });

            migrationBuilder.Sql(AppendOnlyTables.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AppendOnlyTables.Down);

            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "device_group_members");

            migrationBuilder.DropTable(
                name: "notification_deliveries");

            migrationBuilder.DropTable(
                name: "notification_events");

            migrationBuilder.DropTable(
                name: "notification_recipient_states");

            migrationBuilder.DropTable(
                name: "notification_targets");

            migrationBuilder.DropTable(
                name: "platform_administrators");

            migrationBuilder.DropTable(
                name: "tenant_branding");

            migrationBuilder.DropTable(
                name: "tenant_memberships");

            migrationBuilder.DropTable(
                name: "worker_heartbeats");

            migrationBuilder.DropTable(
                name: "device_groups");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "recipients");

            migrationBuilder.DropTable(
                name: "assets");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}
