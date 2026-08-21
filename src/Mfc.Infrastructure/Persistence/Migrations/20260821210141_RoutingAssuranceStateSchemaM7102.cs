using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoutingAssuranceStateSchemaM7102 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "routing_assurance_states",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    OperationalHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "jsonb", nullable: false),
                    OperationalJson = table.Column<string>(type: "jsonb", nullable: false),
                    RouteExpectationsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    RouteFindingsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    ResolutionTracesJson = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routing_assurance_states", x => x.DeviceId);
                    table.CheckConstraint("ck_routing_assurance_states_config_hash", "octet_length(\"ConfigurationHash\") = 32");
                    table.CheckConstraint("ck_routing_assurance_states_ops_hash", "octet_length(\"OperationalHash\") = 32");
                    table.CheckConstraint("ck_routing_assurance_states_row_version", "\"RowVersion\" > 0");
                    table.ForeignKey(
                        name: "FK_routing_assurance_states_devices_DeviceId",
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
                name: "routing_assurance_states");
        }
    }
}
