using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResponseFeedbackEventsSchemaM7405 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "response_feedback_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    EventCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceIdsJson = table.Column<string>(type: "text", nullable: false),
                    PolicyHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    ArtifactHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    PlanHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    VerificationResults = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    RollbackStatus = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ResidualRisk = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Immutable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_response_feedback_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_response_feedback_events_IncidentId",
                table: "response_feedback_events",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_response_feedback_events_IncidentId_CreatedAtUtc",
                table: "response_feedback_events",
                columns: new[] { "IncidentId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_response_feedback_events_NodeId",
                table: "response_feedback_events",
                column: "NodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "response_feedback_events");
        }
    }
}
