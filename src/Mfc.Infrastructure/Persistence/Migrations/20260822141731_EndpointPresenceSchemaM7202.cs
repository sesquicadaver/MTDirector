using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EndpointPresenceSchemaM7202 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "endpoint_presence_intervals",
                columns: table => new
                {
                    PresenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EndpointId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    VlanId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Vrf = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SourceAddress = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MacAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AttributionCertainty = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_endpoint_presence_intervals", x => x.PresenceId);
                    table.CheckConstraint("ck_endpoint_presence_intervals_validity", "\"ValidUntil\" IS NULL OR \"ValidUntil\" > \"ValidFrom\"");
                });

            migrationBuilder.CreateTable(
                name: "endpoint_routing_contexts",
                columns: table => new
                {
                    PresenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EndpointId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    VlanId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Vrf = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SourceAddress = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CorporateRouteTraceJson = table.Column<string>(type: "jsonb", nullable: true),
                    InternetRouteTraceJson = table.Column<string>(type: "jsonb", nullable: true),
                    WazuhRouteTraceJson = table.Column<string>(type: "jsonb", nullable: true),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_endpoint_routing_contexts", x => x.PresenceId);
                    table.CheckConstraint("ck_endpoint_routing_contexts_validity", "\"ValidUntil\" IS NULL OR \"ValidUntil\" > \"ValidFrom\"");
                    table.ForeignKey(
                        name: "FK_endpoint_routing_contexts_endpoint_presence_intervals_Prese~",
                        column: x => x.PresenceId,
                        principalTable: "endpoint_presence_intervals",
                        principalColumn: "PresenceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_endpoint_presence_intervals_EndpointId_ValidFrom",
                table: "endpoint_presence_intervals",
                columns: new[] { "EndpointId", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "ux_endpoint_presence_intervals_active_endpoint",
                table: "endpoint_presence_intervals",
                column: "EndpointId",
                unique: true,
                filter: "\"ValidUntil\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_endpoint_routing_contexts_EndpointId",
                table: "endpoint_routing_contexts",
                column: "EndpointId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "endpoint_routing_contexts");

            migrationBuilder.DropTable(
                name: "endpoint_presence_intervals");
        }
    }
}
