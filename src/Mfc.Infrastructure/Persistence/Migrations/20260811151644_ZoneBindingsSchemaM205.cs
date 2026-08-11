using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ZoneBindingsSchemaM205 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "zone_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerScope = table.Column<short>(type: "smallint", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zone_definitions", x => x.Id);
                    table.CheckConstraint("ck_zone_definitions_key", "length(btrim(\"Key\")) BETWEEN 1 AND 128");
                    table.CheckConstraint("ck_zone_definitions_name", "length(btrim(\"Name\")) BETWEEN 1 AND 128");
                    table.CheckConstraint("ck_zone_definitions_owner_rules", "(\n  (\"OwnerScope\" = 0 AND \"OwnerId\" IS NULL)\n  OR (\"OwnerScope\" IN (1, 2) AND \"OwnerId\" IS NOT NULL)\n)");
                    table.CheckConstraint("ck_zone_definitions_owner_scope", "\"OwnerScope\" BETWEEN 0 AND 2");
                    table.CheckConstraint("ck_zone_definitions_row_version", "\"RowVersion\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "node_zone_bindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    ValuesJson = table.Column<string>(type: "text", nullable: false),
                    ExpectedDependencyHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    LastResolvedDependencyHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    AnalysisStale = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_node_zone_bindings", x => x.Id);
                    table.CheckConstraint("ck_node_zone_bindings_expected_hash", "octet_length(\"ExpectedDependencyHash\") = 32");
                    table.CheckConstraint("ck_node_zone_bindings_kind", "\"Kind\" BETWEEN 0 AND 2");
                    table.CheckConstraint("ck_node_zone_bindings_last_hash", "\"LastResolvedDependencyHash\" IS NULL OR octet_length(\"LastResolvedDependencyHash\") = 32");
                    table.CheckConstraint("ck_node_zone_bindings_row_version", "\"RowVersion\" > 0");
                    table.CheckConstraint("ck_node_zone_bindings_values", "length(btrim(\"ValuesJson\")) > 2");
                    table.ForeignKey(
                        name: "FK_node_zone_bindings_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_node_zone_bindings_zone_definitions_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "zone_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_node_zone_bindings_zone",
                table: "node_zone_bindings",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "uq_node_zone_bindings_node_zone",
                table: "node_zone_bindings",
                columns: new[] { "NodeId", "ZoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_zone_definitions_owner_key",
                table: "zone_definitions",
                columns: new[] { "OwnerScope", "OwnerId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "node_zone_bindings");

            migrationBuilder.DropTable(
                name: "zone_definitions");
        }
    }
}
