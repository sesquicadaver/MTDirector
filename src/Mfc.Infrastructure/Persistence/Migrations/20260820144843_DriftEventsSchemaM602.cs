using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DriftEventsSchemaM602 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "drift_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaselineCommittedHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    ActualManagedResourceHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    DesiredArtifactHashIgnoredForBaseline = table.Column<byte[]>(type: "bytea", nullable: true),
                    Outcome = table.Column<short>(type: "smallint", nullable: false),
                    ConfigurationDriftPresent = table.Column<bool>(type: "boolean", nullable: false),
                    BlocksDeployment = table.Column<bool>(type: "boolean", nullable: false),
                    FindingsJson = table.Column<string>(type: "jsonb", nullable: false),
                    SemanticDiffCanonical = table.Column<string>(type: "text", nullable: true),
                    SemanticDiffHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Immutable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drift_events", x => x.Id);
                    table.CheckConstraint("ck_drift_events_actual", "\"ActualManagedResourceHash\" IS NULL OR octet_length(\"ActualManagedResourceHash\") = 32");
                    table.CheckConstraint("ck_drift_events_baseline", "\"BaselineCommittedHash\" IS NULL OR octet_length(\"BaselineCommittedHash\") = 32");
                    table.CheckConstraint("ck_drift_events_desired", "\"DesiredArtifactHashIgnoredForBaseline\" IS NULL OR octet_length(\"DesiredArtifactHashIgnoredForBaseline\") = 32");
                    table.CheckConstraint("ck_drift_events_immutable", "\"Immutable\" = TRUE");
                    table.CheckConstraint("ck_drift_events_outcome", "\"Outcome\" BETWEEN 0 AND 4");
                    table.CheckConstraint("ck_drift_events_semantic_hash", "\"SemanticDiffHash\" IS NULL OR octet_length(\"SemanticDiffHash\") = 32");
                    table.ForeignKey(
                        name: "FK_drift_events_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_drift_events_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_drift_events_DeviceId",
                table: "drift_events",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_drift_events_DeviceId_CreatedAtUtc",
                table: "drift_events",
                columns: new[] { "DeviceId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_drift_events_NodeId",
                table: "drift_events",
                column: "NodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "drift_events");
        }
    }
}
