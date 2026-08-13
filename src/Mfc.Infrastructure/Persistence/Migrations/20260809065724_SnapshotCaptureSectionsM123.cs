using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotCaptureSectionsM123 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "snapshot_capture_sections",
                columns: table => new
                {
                    CaptureId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<string>(type: "text", nullable: false),
                    SectionVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Ordered = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigurationRecordCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ObservationRecordCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CapabilityRecordCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CompatibilityRecordCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RawHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    ConfigurationHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    ObservationHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    CapabilityHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    CompatibilityHash = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshot_capture_sections", x => new { x.CaptureId, x.SectionId });
                    table.ForeignKey(
                        name: "FK_snapshot_capture_sections_snapshot_captures_CaptureId",
                        column: x => x.CaptureId,
                        principalTable: "snapshot_captures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_snapshot_capture_sections_capability_hash",
                        column: x => x.CapabilityHash,
                        principalTable: "snapshot_payloads",
                        principalColumn: "PayloadHash",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_snapshot_capture_sections_compatibility_hash",
                        column: x => x.CompatibilityHash,
                        principalTable: "snapshot_payloads",
                        principalColumn: "PayloadHash",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_snapshot_capture_sections_configuration_hash",
                        column: x => x.ConfigurationHash,
                        principalTable: "snapshot_payloads",
                        principalColumn: "PayloadHash",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_snapshot_capture_sections_observation_hash",
                        column: x => x.ObservationHash,
                        principalTable: "snapshot_payloads",
                        principalColumn: "PayloadHash",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_snapshot_capture_sections_raw_hash",
                        column: x => x.RawHash,
                        principalTable: "snapshot_payloads",
                        principalColumn: "PayloadHash",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_captures_ConfigurationHash",
                table: "snapshot_captures",
                column: "ConfigurationHash");

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_captures_ObservationHash",
                table: "snapshot_captures",
                column: "ObservationHash");

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_captures_SnapshotHash",
                table: "snapshot_captures",
                column: "SnapshotHash");

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_capture_sections_CapabilityHash",
                table: "snapshot_capture_sections",
                column: "CapabilityHash");

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_capture_sections_CompatibilityHash",
                table: "snapshot_capture_sections",
                column: "CompatibilityHash");

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_capture_sections_ConfigurationHash",
                table: "snapshot_capture_sections",
                column: "ConfigurationHash");

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_capture_sections_ObservationHash",
                table: "snapshot_capture_sections",
                column: "ObservationHash");

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_capture_sections_RawHash",
                table: "snapshot_capture_sections",
                column: "RawHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "snapshot_capture_sections");

            migrationBuilder.DropIndex(
                name: "IX_snapshot_captures_ConfigurationHash",
                table: "snapshot_captures");

            migrationBuilder.DropIndex(
                name: "IX_snapshot_captures_ObservationHash",
                table: "snapshot_captures");

            migrationBuilder.DropIndex(
                name: "IX_snapshot_captures_SnapshotHash",
                table: "snapshot_captures");
        }
    }
}
