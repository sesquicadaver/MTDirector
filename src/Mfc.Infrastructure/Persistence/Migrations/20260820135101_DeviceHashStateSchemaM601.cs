using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeviceHashStateSchemaM601 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_hash_states",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DesiredPolicyHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    DesiredArtifactHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    LastCommittedPolicyHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    LastCommittedArtifactHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    ActualManagedResourceHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    ActualKnown = table.Column<bool>(type: "boolean", nullable: false),
                    AnchorKnown = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_hash_states", x => x.DeviceId);
                    table.CheckConstraint("ck_device_hash_states_actual", "\"ActualManagedResourceHash\" IS NULL OR octet_length(\"ActualManagedResourceHash\") = 32");
                    table.CheckConstraint("ck_device_hash_states_committed_artifact", "\"LastCommittedArtifactHash\" IS NULL OR octet_length(\"LastCommittedArtifactHash\") = 32");
                    table.CheckConstraint("ck_device_hash_states_committed_policy", "\"LastCommittedPolicyHash\" IS NULL OR octet_length(\"LastCommittedPolicyHash\") = 32");
                    table.CheckConstraint("ck_device_hash_states_desired_artifact", "\"DesiredArtifactHash\" IS NULL OR octet_length(\"DesiredArtifactHash\") = 32");
                    table.CheckConstraint("ck_device_hash_states_desired_policy", "\"DesiredPolicyHash\" IS NULL OR octet_length(\"DesiredPolicyHash\") = 32");
                    table.CheckConstraint("ck_device_hash_states_row_version", "\"RowVersion\" > 0");
                    table.ForeignKey(
                        name: "FK_device_hash_states_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_hash_states");
        }
    }
}
