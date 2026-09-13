using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class DeploymentLockExpiryAllowsEqualW7226 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_deployment_locks_expiry",
            table: "deployment_locks");

        migrationBuilder.AddCheckConstraint(
            name: "ck_deployment_locks_expiry",
            table: "deployment_locks",
            sql: "\"ExpiresAtUtc\" >= \"AcquiredAtUtc\"");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_deployment_locks_expiry",
            table: "deployment_locks");

        migrationBuilder.AddCheckConstraint(
            name: "ck_deployment_locks_expiry",
            table: "deployment_locks",
            sql: "\"ExpiresAtUtc\" > \"AcquiredAtUtc\"");
    }
}
