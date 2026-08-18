using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OnboardingSchemaM501 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "ManagementState",
                table: "nodes",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "ManagementState",
                table: "devices",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.CreateTable(
                name: "onboarding_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeMembershipHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    TopologyProjectionHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PlanHash = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_plans", x => x.Id);
                    table.CheckConstraint("ck_onboarding_plans_lifetime", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
                    table.CheckConstraint("ck_onboarding_plans_membership_hash", "octet_length(\"NodeMembershipHash\") = 32");
                    table.CheckConstraint("ck_onboarding_plans_plan_hash", "octet_length(\"PlanHash\") = 32");
                    table.CheckConstraint("ck_onboarding_plans_topology_hash", "octet_length(\"TopologyProjectionHash\") = 32");
                    table.ForeignKey(
                        name: "FK_onboarding_plans_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "onboarding_device_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedRouterOsVersion = table.Column<string>(type: "text", nullable: false),
                    ExpectedCapabilityHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ExpectedConfigurationHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ExpectedCompatibilityHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ExpectedApiServiceHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ExpectedReadAccountHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ExpectedDeploymentAccountHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ExpectedDeviceModeHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ExpectedGuardHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    RequiredAnchorSetJson = table.Column<string>(type: "text", nullable: false),
                    BootstrapArtifactHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    WatchdogTtlSeconds = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_device_plans", x => x.Id);
                    table.CheckConstraint("ck_onboarding_device_plans_anchors", "length(btrim(\"RequiredAnchorSetJson\")) >= 2");
                    table.CheckConstraint("ck_onboarding_device_plans_api_hash", "octet_length(\"ExpectedApiServiceHash\") = 32");
                    table.CheckConstraint("ck_onboarding_device_plans_bootstrap_hash", "octet_length(\"BootstrapArtifactHash\") = 32");
                    table.CheckConstraint("ck_onboarding_device_plans_cap_hash", "octet_length(\"ExpectedCapabilityHash\") = 32");
                    table.CheckConstraint("ck_onboarding_device_plans_cfg_hash", "octet_length(\"ExpectedConfigurationHash\") = 32");
                    table.CheckConstraint("ck_onboarding_device_plans_compat_hash", "octet_length(\"ExpectedCompatibilityHash\") = 32");
                    table.CheckConstraint("ck_onboarding_device_plans_deploy_hash", "octet_length(\"ExpectedDeploymentAccountHash\") = 32");
                    table.CheckConstraint("ck_onboarding_device_plans_guard_hash", "octet_length(\"ExpectedGuardHash\") = 32");
                    table.CheckConstraint("ck_onboarding_device_plans_mode_hash", "octet_length(\"ExpectedDeviceModeHash\") = 32");
                    table.CheckConstraint("ck_onboarding_device_plans_read_hash", "octet_length(\"ExpectedReadAccountHash\") = 32");
                    table.CheckConstraint("ck_onboarding_device_plans_version", "length(btrim(\"ExpectedRouterOsVersion\")) BETWEEN 1 AND 128");
                    table.CheckConstraint("ck_onboarding_device_plans_watchdog_ttl", "\"WatchdogTtlSeconds\" BETWEEN 60 AND 600");
                    table.ForeignKey(
                        name: "FK_onboarding_device_plans_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_onboarding_device_plans_onboarding_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "onboarding_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "onboarding_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<short>(type: "smallint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_operations", x => x.Id);
                    table.CheckConstraint("ck_onboarding_operations_row_version", "\"RowVersion\" > 0");
                    table.CheckConstraint("ck_onboarding_operations_state", "\"State\" BETWEEN 0 AND 13");
                    table.CheckConstraint("ck_onboarding_operations_terminal_completed", "(\"State\" IN (8, 11, 12, 13) AND \"CompletedAtUtc\" IS NOT NULL) OR (\"State\" NOT IN (8, 11, 12, 13))");
                    table.ForeignKey(
                        name: "FK_onboarding_operations_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_onboarding_operations_onboarding_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "onboarding_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "onboarding_anchor_placements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DevicePlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Family = table.Column<short>(type: "smallint", nullable: false),
                    Chain = table.Column<short>(type: "smallint", nullable: false),
                    Mode = table.Column<short>(type: "smallint", nullable: false),
                    ReferenceRuleFingerprint = table.Column<byte[]>(type: "bytea", nullable: true),
                    ReferenceOccurrenceRank = table.Column<long>(type: "bigint", nullable: true),
                    ExpectedPredecessorFingerprint = table.Column<byte[]>(type: "bytea", nullable: true),
                    ExpectedSuccessorFingerprint = table.Column<byte[]>(type: "bytea", nullable: true),
                    ExpectedAnchorOrdinal = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_anchor_placements", x => x.Id);
                    table.CheckConstraint("ck_onboarding_anchor_placements_before_ref", "(\"Mode\" = 0 AND \"ReferenceRuleFingerprint\" IS NOT NULL AND \"ReferenceOccurrenceRank\" IS NOT NULL) OR (\"Mode\" = 1 AND \"ReferenceRuleFingerprint\" IS NULL AND \"ReferenceOccurrenceRank\" IS NULL)");
                    table.CheckConstraint("ck_onboarding_anchor_placements_chain", "\"Chain\" BETWEEN 0 AND 2");
                    table.CheckConstraint("ck_onboarding_anchor_placements_family", "\"Family\" BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_onboarding_anchor_placements_mode", "\"Mode\" BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_onboarding_anchor_placements_ordinal", "\"ExpectedAnchorOrdinal\" >= 0");
                    table.CheckConstraint("ck_onboarding_anchor_placements_pred_hash", "\"ExpectedPredecessorFingerprint\" IS NULL OR octet_length(\"ExpectedPredecessorFingerprint\") = 32");
                    table.CheckConstraint("ck_onboarding_anchor_placements_ref_hash", "\"ReferenceRuleFingerprint\" IS NULL OR octet_length(\"ReferenceRuleFingerprint\") = 32");
                    table.CheckConstraint("ck_onboarding_anchor_placements_succ_hash", "\"ExpectedSuccessorFingerprint\" IS NULL OR octet_length(\"ExpectedSuccessorFingerprint\") = 32");
                    table.ForeignKey(
                        name: "FK_onboarding_anchor_placements_onboarding_device_plans_Device~",
                        column: x => x.DevicePlanId,
                        principalTable: "onboarding_device_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "onboarding_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    ExpectedBeforeHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    DesiredAfterHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    State = table.Column<short>(type: "smallint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_steps", x => x.Id);
                    table.CheckConstraint("ck_onboarding_steps_after_hash", "octet_length(\"DesiredAfterHash\") = 32");
                    table.CheckConstraint("ck_onboarding_steps_before_hash", "octet_length(\"ExpectedBeforeHash\") = 32");
                    table.CheckConstraint("ck_onboarding_steps_kind", "\"Kind\" BETWEEN 0 AND 13");
                    table.CheckConstraint("ck_onboarding_steps_sequence", "\"Sequence\" > 0");
                    table.CheckConstraint("ck_onboarding_steps_state", "\"State\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_onboarding_steps_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_onboarding_steps_onboarding_operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "onboarding_operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_nodes_management_state",
                table: "nodes",
                sql: "\"ManagementState\" BETWEEN 0 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "ck_devices_management_state",
                table: "devices",
                sql: "\"ManagementState\" BETWEEN 0 AND 2");

            migrationBuilder.CreateIndex(
                name: "uq_onboarding_anchor_placements_plan_key",
                table: "onboarding_anchor_placements",
                columns: new[] { "DevicePlanId", "Family", "Chain" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_onboarding_device_plans_DeviceId",
                table: "onboarding_device_plans",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "uq_onboarding_device_plans_plan_device",
                table: "onboarding_device_plans",
                columns: new[] { "PlanId", "DeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_onboarding_operations_plan",
                table: "onboarding_operations",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "uq_onboarding_operations_node_nonterminal",
                table: "onboarding_operations",
                column: "NodeId",
                unique: true,
                filter: "\"State\" NOT IN (8, 11, 12, 13)");

            migrationBuilder.CreateIndex(
                name: "ix_onboarding_plans_node",
                table: "onboarding_plans",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_onboarding_steps_DeviceId",
                table: "onboarding_steps",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "uq_onboarding_steps_operation_sequence",
                table: "onboarding_steps",
                columns: new[] { "OperationId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "onboarding_anchor_placements");

            migrationBuilder.DropTable(
                name: "onboarding_steps");

            migrationBuilder.DropTable(
                name: "onboarding_device_plans");

            migrationBuilder.DropTable(
                name: "onboarding_operations");

            migrationBuilder.DropTable(
                name: "onboarding_plans");

            migrationBuilder.DropCheckConstraint(
                name: "ck_nodes_management_state",
                table: "nodes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_devices_management_state",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "ManagementState",
                table: "nodes");

            migrationBuilder.DropColumn(
                name: "ManagementState",
                table: "devices");
        }
    }
}
