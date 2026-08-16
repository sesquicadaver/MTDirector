using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PolicyApprovalBindingSchemaM217 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "policy_analysis_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionContentHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    LogicalEffectiveHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    AnalysisContextHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    EvidenceContextHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    TopologyProjectionHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ImpactSetHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    PerDeviceAnalysisHashes = table.Column<byte[]>(type: "bytea", nullable: false),
                    BundleHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    DependencyFingerprint = table.Column<byte[]>(type: "bytea", nullable: false),
                    RiskLevel = table.Column<string>(type: "text", nullable: false),
                    EvidenceSignalsPresent = table.Column<bool>(type: "boolean", nullable: false),
                    AnalyzerVersion = table.Column<string>(type: "text", nullable: false),
                    PolicySchemaVersion = table.Column<string>(type: "text", nullable: false),
                    PipelineVersion = table.Column<string>(type: "text", nullable: false),
                    FindingsJson = table.Column<string>(type: "text", nullable: false),
                    TestResultsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_analysis_runs", x => x.Id);
                    table.CheckConstraint("ck_policy_analysis_runs_analysis_hash", "octet_length(\"AnalysisContextHash\") = 32");
                    table.CheckConstraint("ck_policy_analysis_runs_bundle_hash", "octet_length(\"BundleHash\") = 32");
                    table.CheckConstraint("ck_policy_analysis_runs_deps_hash", "octet_length(\"DependencyFingerprint\") = 32");
                    table.CheckConstraint("ck_policy_analysis_runs_device_hashes", "octet_length(\"PerDeviceAnalysisHashes\") % 32 = 0");
                    table.CheckConstraint("ck_policy_analysis_runs_evidence_hash", "octet_length(\"EvidenceContextHash\") = 32");
                    table.CheckConstraint("ck_policy_analysis_runs_findings", "length(btrim(\"FindingsJson\")) >= 2");
                    table.CheckConstraint("ck_policy_analysis_runs_impact_hash", "octet_length(\"ImpactSetHash\") = 32");
                    table.CheckConstraint("ck_policy_analysis_runs_logical_hash", "octet_length(\"LogicalEffectiveHash\") = 32");
                    table.CheckConstraint("ck_policy_analysis_runs_revision_hash", "octet_length(\"RevisionContentHash\") = 32");
                    table.CheckConstraint("ck_policy_analysis_runs_tests", "length(btrim(\"TestResultsJson\")) >= 2");
                    table.CheckConstraint("ck_policy_analysis_runs_topology_hash", "octet_length(\"TopologyProjectionHash\") = 32");
                    table.ForeignKey(
                        name: "FK_policy_analysis_runs_policy_revisions_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "policy_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "policy_approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    BundleHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsSecurityOwner = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_approvals", x => x.Id);
                    table.CheckConstraint("ck_policy_approvals_bundle_hash", "octet_length(\"BundleHash\") = 32");
                    table.ForeignKey(
                        name: "FK_policy_approvals_policy_analysis_runs_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "policy_analysis_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_policy_approvals_policy_revisions_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "policy_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "policy_bindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<short>(type: "smallint", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DesiredRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    BundleHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    State = table.Column<short>(type: "smallint", nullable: false),
                    ValidFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ValidUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_bindings", x => x.Id);
                    table.CheckConstraint("ck_policy_bindings_bundle_hash", "octet_length(\"BundleHash\") = 32");
                    table.CheckConstraint("ck_policy_bindings_row_version", "\"RowVersion\" > 0");
                    table.CheckConstraint("ck_policy_bindings_scope", "\"Scope\" BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_policy_bindings_scope_id", "(\"Scope\" = 0 AND \"ScopeId\" IS NULL) OR (\"Scope\" <> 0 AND \"ScopeId\" IS NOT NULL)");
                    table.CheckConstraint("ck_policy_bindings_state", "\"State\" BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_policy_bindings_policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_policy_bindings_policy_analysis_runs_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "policy_analysis_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_policy_bindings_policy_revisions_DesiredRevisionId",
                        column: x => x.DesiredRevisionId,
                        principalTable: "policy_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warning_acknowledgments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarningHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    AcknowledgedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warning_acknowledgments", x => x.Id);
                    table.CheckConstraint("ck_warning_acknowledgments_hash", "octet_length(\"WarningHash\") = 32");
                    table.ForeignKey(
                        name: "FK_warning_acknowledgments_policy_analysis_runs_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "policy_analysis_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_policy_analysis_runs_revision",
                table: "policy_analysis_runs",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_policy_approvals_AnalysisRunId",
                table: "policy_approvals",
                column: "AnalysisRunId");

            migrationBuilder.CreateIndex(
                name: "uq_policy_approvals_revision_reviewer_run",
                table: "policy_approvals",
                columns: new[] { "RevisionId", "ReviewerId", "AnalysisRunId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_policy_bindings_AnalysisRunId",
                table: "policy_bindings",
                column: "AnalysisRunId");

            migrationBuilder.CreateIndex(
                name: "IX_policy_bindings_PolicyId",
                table: "policy_bindings",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "ix_policy_bindings_revision",
                table: "policy_bindings",
                column: "DesiredRevisionId");

            migrationBuilder.CreateIndex(
                name: "uq_policy_bindings_company_active",
                table: "policy_bindings",
                column: "Scope",
                unique: true,
                filter: "\"State\" = 0 AND \"Scope\" = 0");

            migrationBuilder.CreateIndex(
                name: "uq_policy_bindings_overlay_active",
                table: "policy_bindings",
                columns: new[] { "Scope", "ScopeId" },
                unique: true,
                filter: "\"State\" = 0 AND \"Scope\" IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "uq_policy_bindings_exception_policy_active",
                table: "policy_bindings",
                column: "PolicyId",
                unique: true,
                filter: "\"State\" = 0 AND \"Scope\" = 3");

            migrationBuilder.Sql(
                """
                CREATE FUNCTION mfc_enforce_exception_binding_cap()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  IF NEW."State" = 0 AND NEW."Scope" = 3 THEN
                    IF (
                      SELECT COUNT(*)
                      FROM policy_bindings
                      WHERE "State" = 0
                        AND "Scope" = 3
                        AND "ScopeId" IS NOT DISTINCT FROM NEW."ScopeId"
                        AND "Id" <> NEW."Id"
                    ) >= 256 THEN
                      RAISE EXCEPTION 'POLICY_BINDING_CARDINALITY'
                        USING ERRCODE = '23514';
                    END IF;
                  END IF;
                  RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_policy_bindings_exception_cap
                BEFORE INSERT OR UPDATE OF "State", "Scope", "ScopeId"
                ON policy_bindings
                FOR EACH ROW
                EXECUTE FUNCTION mfc_enforce_exception_binding_cap();
                """);

            migrationBuilder.CreateIndex(
                name: "uq_warning_acknowledgments_run_hash_actor",
                table: "warning_acknowledgments",
                columns: new[] { "AnalysisRunId", "WarningHash", "AcknowledgedBy" },
                unique: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedAnalysisRunId",
                table: "policy_revisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ApprovedBundleHash",
                table: "policy_revisions",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_policy_revisions_approved_analysis",
                table: "policy_revisions",
                sql: "(\"ApprovedAnalysisRunId\" IS NULL AND \"ApprovedBundleHash\" IS NULL) OR (\"ApprovedAnalysisRunId\" IS NOT NULL AND octet_length(\"ApprovedBundleHash\") = 32)");

            migrationBuilder.CreateIndex(
                name: "IX_policy_revisions_ApprovedAnalysisRunId",
                table: "policy_revisions",
                column: "ApprovedAnalysisRunId");

            migrationBuilder.AddForeignKey(
                name: "FK_policy_revisions_policy_analysis_runs_ApprovedAnalysisRunId",
                table: "policy_revisions",
                column: "ApprovedAnalysisRunId",
                principalTable: "policy_analysis_runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_policy_revisions_policy_analysis_runs_ApprovedAnalysisRunId",
                table: "policy_revisions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_policy_revisions_approved_analysis",
                table: "policy_revisions");

            migrationBuilder.DropIndex(
                name: "IX_policy_revisions_ApprovedAnalysisRunId",
                table: "policy_revisions");

            migrationBuilder.DropColumn(
                name: "ApprovedAnalysisRunId",
                table: "policy_revisions");

            migrationBuilder.DropColumn(
                name: "ApprovedBundleHash",
                table: "policy_revisions");

            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_policy_bindings_exception_cap ON policy_bindings;
                DROP FUNCTION IF EXISTS mfc_enforce_exception_binding_cap();
                """);

            migrationBuilder.DropTable(
                name: "policy_approvals");

            migrationBuilder.DropTable(
                name: "policy_bindings");

            migrationBuilder.DropTable(
                name: "warning_acknowledgments");

            migrationBuilder.DropTable(
                name: "policy_analysis_runs");
        }
    }
}
