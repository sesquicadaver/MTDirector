using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditEventPreviousHashUniqueSec03 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_audit_events_PreviousEventHash_unique",
                table: "audit_events",
                column: "PreviousEventHash",
                unique: true,
                filter: "\"PreviousEventHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_events_PreviousEventHash_unique",
                table: "audit_events");
        }
    }
}
