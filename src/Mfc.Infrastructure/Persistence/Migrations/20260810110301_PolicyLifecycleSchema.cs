using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PolicyLifecycleSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    OwnerScope = table.Column<short>(type: "smallint", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policies", x => x.Id);
                    table.CheckConstraint("ck_policies_kind", "\"Kind\" BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_policies_name", "length(btrim(\"Name\")) BETWEEN 1 AND 128");
                    table.CheckConstraint("ck_policies_owner_rules", "(\n  (\"Kind\" = 0 AND \"OwnerScope\" = 0 AND \"OwnerId\" IS NULL)\n  OR (\"Kind\" = 1 AND \"OwnerScope\" = 1 AND \"OwnerId\" IS NOT NULL)\n  OR (\"Kind\" = 2 AND \"OwnerScope\" = 2 AND \"OwnerId\" IS NOT NULL)\n  OR (\"Kind\" = 3 AND \"OwnerScope\" IN (1, 2) AND \"OwnerId\" IS NOT NULL)\n)");
                    table.CheckConstraint("ck_policies_owner_scope", "\"OwnerScope\" BETWEEN 0 AND 2");
                    table.CheckConstraint("ck_policies_row_version", "\"RowVersion\" > 0");
                    table.CheckConstraint("ck_policies_status", "\"Status\" BETWEEN 0 AND 1");
                });

            migrationBuilder.CreateTable(
                name: "policy_revisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<long>(type: "bigint", nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    ContentHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ParentContextHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    State = table.Column<short>(type: "smallint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Compression = table.Column<short>(type: "smallint", nullable: false),
                    UncompressedSize = table.Column<long>(type: "bigint", nullable: false),
                    CompressedPayload = table.Column<byte[]>(type: "bytea", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_revisions", x => x.Id);
                    table.CheckConstraint("ck_policy_revisions_approved_at", "(\"State\" = 3 AND \"ApprovedAtUtc\" IS NOT NULL) OR (\"State\" <> 3)");
                    table.CheckConstraint("ck_policy_revisions_content_hash", "octet_length(\"ContentHash\") = 32");
                    table.CheckConstraint("ck_policy_revisions_parent_hash", "\"ParentContextHash\" IS NULL OR octet_length(\"ParentContextHash\") = 32");
                    table.CheckConstraint("ck_policy_revisions_revision_number", "\"RevisionNumber\" > 0");
                    table.CheckConstraint("ck_policy_revisions_schema_version", "\"SchemaVersion\" > 0");
                    table.CheckConstraint("ck_policy_revisions_size", "\"UncompressedSize\" > 0 AND \"UncompressedSize\" <= 268435456");
                    table.CheckConstraint("ck_policy_revisions_state", "\"State\" BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_policy_revisions_policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_policies_kind_owner",
                table: "policies",
                columns: new[] { "Kind", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "ix_policy_revisions_content_hash",
                table: "policy_revisions",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "uq_policy_revisions_policy_revision",
                table: "policy_revisions",
                columns: new[] { "PolicyId", "RevisionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "policy_revisions");

            migrationBuilder.DropTable(
                name: "policies");
        }
    }
}
