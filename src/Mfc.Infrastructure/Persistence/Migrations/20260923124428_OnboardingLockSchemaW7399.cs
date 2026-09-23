using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OnboardingLockSchemaW7399 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "onboarding_locks",
                columns: table => new
                {
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerInstanceId = table.Column<string>(type: "text", nullable: false),
                    AcquiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HeartbeatAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_locks", x => x.NodeId);
                    table.CheckConstraint("ck_onboarding_locks_expiry", "\"ExpiresAtUtc\" >= \"AcquiredAtUtc\"");
                    table.CheckConstraint("ck_onboarding_locks_owner", "length(btrim(\"OwnerInstanceId\")) BETWEEN 1 AND 128");
                    table.ForeignKey(
                        name: "FK_onboarding_locks_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_onboarding_locks_onboarding_operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "onboarding_operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_onboarding_locks_OperationId",
                table: "onboarding_locks",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "uq_onboarding_locks_node",
                table: "onboarding_locks",
                column: "NodeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "onboarding_locks");
        }
    }
}
