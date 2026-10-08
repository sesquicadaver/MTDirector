using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaptureIdempotencyTargetIdUniqueW7436 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_capture_operation_idempotency",
                table: "capture_operations");

            migrationBuilder.CreateIndex(
                name: "uq_capture_operation_idempotency",
                table: "capture_operations",
                columns: new[] { "RequestedBy", "IdempotencyKey", "TargetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_capture_operation_idempotency",
                table: "capture_operations");

            migrationBuilder.CreateIndex(
                name: "uq_capture_operation_idempotency",
                table: "capture_operations",
                columns: new[] { "RequestedBy", "IdempotencyKey" },
                unique: true);
        }
    }
}
