using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InventorySnapshotSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "capture_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<short>(type: "smallint", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_capture_operations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sites", x => x.Id);
                    table.CheckConstraint("ck_sites_code", "\"Code\" ~ '^[A-Z][A-Z0-9_-]{1,31}$'");
                    table.CheckConstraint("ck_sites_name", "length(btrim(\"Name\")) BETWEEN 1 AND 128");
                    table.CheckConstraint("ck_sites_row_version", "\"RowVersion\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "snapshot_payloads",
                columns: table => new
                {
                    PayloadHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    PayloadKind = table.Column<short>(type: "smallint", nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    Compression = table.Column<short>(type: "smallint", nullable: false),
                    UncompressedSize = table.Column<long>(type: "bigint", nullable: false),
                    CompressedPayload = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshot_payloads", x => x.PayloadHash);
                    table.CheckConstraint("ck_snapshot_payload_hash", "octet_length(\"PayloadHash\") = 32");
                    table.CheckConstraint("ck_snapshot_payload_size", "\"UncompressedSize\" > 0 AND \"UncompressedSize\" <= 268435456");
                });

            migrationBuilder.CreateTable(
                name: "nodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DeclaredKind = table.Column<short>(type: "smallint", nullable: false),
                    DeclaredUplinkMode = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nodes", x => x.Id);
                    table.CheckConstraint("ck_nodes_name", "length(btrim(\"Name\")) BETWEEN 1 AND 128");
                    table.CheckConstraint("ck_nodes_row_version", "\"RowVersion\" > 0");
                    table.ForeignKey(
                        name: "FK_nodes_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    ManagementHost = table.Column<string>(type: "text", nullable: false),
                    ManagementHostKind = table.Column<short>(type: "smallint", nullable: false),
                    ManagementPort = table.Column<int>(type: "integer", nullable: false, defaultValue: 8729),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastSupportState = table.Column<short>(type: "smallint", nullable: true),
                    LastCompletedCaptureId = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.Id);
                    table.CheckConstraint("ck_devices_name", "length(btrim(\"DisplayName\")) BETWEEN 1 AND 128");
                    table.CheckConstraint("ck_devices_port", "\"ManagementPort\" BETWEEN 1 AND 65535");
                    table.CheckConstraint("ck_devices_row_version", "\"RowVersion\" > 0");
                    table.ForeignKey(
                        name: "FK_devices_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "device_connection_profiles",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    EncryptedSecretId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrustMode = table.Column<short>(type: "smallint", nullable: false),
                    CaProfileRef = table.Column<string>(type: "text", nullable: true),
                    PinnedSpkiSha256 = table.Column<byte[]>(type: "bytea", nullable: true),
                    ConnectTimeoutMs = table.Column<int>(type: "integer", nullable: false),
                    CommandTimeoutMs = table.Column<int>(type: "integer", nullable: false),
                    MaxResponseBytes = table.Column<long>(type: "bigint", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_connection_profiles", x => x.DeviceId);
                    table.CheckConstraint("ck_connection_command_timeout", "\"CommandTimeoutMs\" BETWEEN 1000 AND 120000");
                    table.CheckConstraint("ck_connection_connect_timeout", "\"ConnectTimeoutMs\" BETWEEN 1000 AND 30000");
                    table.CheckConstraint("ck_connection_max_response", "\"MaxResponseBytes\" BETWEEN 1048576 AND 268435456");
                    table.CheckConstraint("ck_connection_row_version", "\"RowVersion\" > 0");
                    table.CheckConstraint("ck_connection_spki", "\"PinnedSpkiSha256\" IS NULL OR octet_length(\"PinnedSpkiSha256\") = 32");
                    table.CheckConstraint("ck_connection_username", "length(\"Username\") BETWEEN 1 AND 64");
                    table.ForeignKey(
                        name: "FK_device_connection_profiles_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_device_connection_profiles_encrypted_secrets_EncryptedSecre~",
                        column: x => x.EncryptedSecretId,
                        principalTable: "encrypted_secrets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "snapshot_captures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    AttemptCount = table.Column<short>(type: "smallint", nullable: false),
                    CaptureStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Pass1CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Pass2CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CaptureCompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RawPayloadHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    ConfigurationPayloadHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    ObservationPayloadHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    CapabilityPayloadHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    CompatibilityPayloadHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    ConfigurationHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    ObservationHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    CapabilityHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    CompatibilityMaterialHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    SnapshotHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    SectionResultsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    ErrorDetailsJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshot_captures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_snapshot_captures_capture_operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "capture_operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_snapshot_captures_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_capture_operation_idempotency",
                table: "capture_operations",
                columns: new[] { "RequestedBy", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_connection_profiles_EncryptedSecretId",
                table: "device_connection_profiles",
                column: "EncryptedSecretId");

            migrationBuilder.CreateIndex(
                name: "IX_devices_NodeId",
                table: "devices",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "uq_devices_active_endpoint",
                table: "devices",
                columns: new[] { "ManagementHost", "ManagementPort" },
                unique: true,
                filter: "\"Enabled\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "uq_nodes_site_name",
                table: "nodes",
                columns: new[] { "SiteId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_sites_code",
                table: "sites",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_captures_DeviceId",
                table: "snapshot_captures",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "uq_snapshot_capture_device_operation",
                table: "snapshot_captures",
                columns: new[] { "OperationId", "DeviceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_connection_profiles");

            migrationBuilder.DropTable(
                name: "snapshot_captures");

            migrationBuilder.DropTable(
                name: "snapshot_payloads");

            migrationBuilder.DropTable(
                name: "capture_operations");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "nodes");

            migrationBuilder.DropTable(
                name: "sites");
        }
    }
}
