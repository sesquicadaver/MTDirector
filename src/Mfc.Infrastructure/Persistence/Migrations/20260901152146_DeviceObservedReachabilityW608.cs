using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeviceObservedReachabilityW608 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "LastObservedReachability",
                table: "devices",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_devices_last_observed_reachability",
                table: "devices",
                sql: "\"LastObservedReachability\" IS NULL OR \"LastObservedReachability\" BETWEEN 0 AND 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_devices_last_observed_reachability",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "LastObservedReachability",
                table: "devices");
        }
    }
}
