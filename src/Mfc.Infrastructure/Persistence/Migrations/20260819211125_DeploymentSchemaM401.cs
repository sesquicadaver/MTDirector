using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeploymentSchemaM401 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "deployment_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogicalPolicyHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    AnalysisBundleHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    TopologyProjectionHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ActivationOrderJson = table.Column<string>(type: "text", nullable: false),
                    RollbackOrderJson = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PlanHash = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deployment_plans", x => x.Id);
                    table.CheckConstraint("ck_deployment_plans_activation", "length(btrim(\"ActivationOrderJson\")) >= 2");
                    table.CheckConstraint("ck_deployment_plans_analysis_hash", "octet_length(\"AnalysisBundleHash\") = 32");
                    table.CheckConstraint("ck_deployment_plans_lifetime", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
                    table.CheckConstraint("ck_deployment_plans_plan_hash", "octet_length(\"PlanHash\") = 32");
                    table.CheckConstraint("ck_deployment_plans_policy_hash", "octet_length(\"LogicalPolicyHash\") = 32");
                    table.CheckConstraint("ck_deployment_plans_rollback", "length(btrim(\"RollbackOrderJson\")) >= 2");
                    table.CheckConstraint("ck_deployment_plans_topology_hash", "octet_length(\"TopologyProjectionHash\") = 32");
                    table.ForeignKey(
                        name: "FK_deployment_plans_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deployment_device_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedRouterOsVersion = table.Column<string>(type: "text", nullable: false),
                    ExpectedCapabilityHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ExpectedConfigurationHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ExpectedCompatibilityHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ExpectedGuardContextHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ExpectedAnchorContextHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    OldArtifactHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    NewArtifactHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    OldAnchorTargetsJson = table.Column<string>(type: "text", nullable: false),
                    NewAnchorTargetsJson = table.Column<string>(type: "text", nullable: false),
                    AnchorActivationOrderJson = table.Column<string>(type: "text", nullable: false),
                    AnchorRollbackOrderJson = table.Column<string>(type: "text", nullable: false),
                    TransitionStateHashesJson = table.Column<string>(type: "text", nullable: false),
                    RollbackTtlSeconds = table.Column<int>(type: "integer", nullable: false),
                    ProbesJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deployment_device_plans", x => x.Id);
                    table.CheckConstraint("ck_deployment_device_plans_act_order", "length(btrim(\"AnchorActivationOrderJson\")) >= 2");
                    table.CheckConstraint("ck_deployment_device_plans_anchor_hash", "octet_length(\"ExpectedAnchorContextHash\") = 32");
                    table.CheckConstraint("ck_deployment_device_plans_cap_hash", "octet_length(\"ExpectedCapabilityHash\") = 32");
                    table.CheckConstraint("ck_deployment_device_plans_cfg_hash", "octet_length(\"ExpectedConfigurationHash\") = 32");
                    table.CheckConstraint("ck_deployment_device_plans_compat_hash", "octet_length(\"ExpectedCompatibilityHash\") = 32");
                    table.CheckConstraint("ck_deployment_device_plans_guard_hash", "octet_length(\"ExpectedGuardContextHash\") = 32");
                    table.CheckConstraint("ck_deployment_device_plans_new_art", "octet_length(\"NewArtifactHash\") = 32");
                    table.CheckConstraint("ck_deployment_device_plans_new_targets", "length(btrim(\"NewAnchorTargetsJson\")) >= 2");
                    table.CheckConstraint("ck_deployment_device_plans_old_art", "octet_length(\"OldArtifactHash\") = 32");
                    table.CheckConstraint("ck_deployment_device_plans_old_targets", "length(btrim(\"OldAnchorTargetsJson\")) >= 2");
                    table.CheckConstraint("ck_deployment_device_plans_probes", "length(btrim(\"ProbesJson\")) >= 2");
                    table.CheckConstraint("ck_deployment_device_plans_rb_order", "length(btrim(\"AnchorRollbackOrderJson\")) >= 2");
                    table.CheckConstraint("ck_deployment_device_plans_rollback_ttl", "\"RollbackTtlSeconds\" BETWEEN 60 AND 600");
                    table.CheckConstraint("ck_deployment_device_plans_transitions", "length(btrim(\"TransitionStateHashesJson\")) >= 2");
                    table.CheckConstraint("ck_deployment_device_plans_version", "length(btrim(\"ExpectedRouterOsVersion\")) BETWEEN 1 AND 128");
                    table.ForeignKey(
                        name: "FK_deployment_device_plans_deployment_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "deployment_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deployment_device_plans_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deployment_operations",
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
                    table.PrimaryKey("PK_deployment_operations", x => x.Id);
                    table.CheckConstraint("ck_deployment_operations_row_version", "\"RowVersion\" > 0");
                    table.CheckConstraint("ck_deployment_operations_state", "\"State\" BETWEEN 0 AND 17");
                    table.CheckConstraint("ck_deployment_operations_terminal_completed", "(\"State\" IN (9, 12, 13, 14, 15, 16, 17) AND \"CompletedAtUtc\" IS NOT NULL) OR (\"State\" NOT IN (9, 12, 13, 14, 15, 16, 17))");
                    table.ForeignKey(
                        name: "FK_deployment_operations_deployment_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "deployment_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deployment_operations_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deployment_device_states",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<short>(type: "smallint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deployment_device_states", x => new { x.OperationId, x.DeviceId });
                    table.CheckConstraint("ck_deployment_device_states_state", "\"State\" BETWEEN 0 AND 12");
                    table.ForeignKey(
                        name: "FK_deployment_device_states_deployment_operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "deployment_operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deployment_device_states_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deployment_locks",
                columns: table => new
                {
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerInstanceId = table.Column<string>(type: "text", nullable: false),
                    AcquiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HeartbeatAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deployment_locks", x => x.NodeId);
                    table.CheckConstraint("ck_deployment_locks_expiry", "\"ExpiresAtUtc\" > \"AcquiredAtUtc\"");
                    table.CheckConstraint("ck_deployment_locks_owner", "length(btrim(\"OwnerInstanceId\")) BETWEEN 1 AND 128");
                    table.ForeignKey(
                        name: "FK_deployment_locks_deployment_operations_DeploymentId",
                        column: x => x.DeploymentId,
                        principalTable: "deployment_operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deployment_locks_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deployment_steps",
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
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SanitizedError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deployment_steps", x => x.Id);
                    table.CheckConstraint("ck_deployment_steps_after_hash", "octet_length(\"DesiredAfterHash\") = 32");
                    table.CheckConstraint("ck_deployment_steps_before_hash", "octet_length(\"ExpectedBeforeHash\") = 32");
                    table.CheckConstraint("ck_deployment_steps_kind", "\"Kind\" BETWEEN 0 AND 10");
                    table.CheckConstraint("ck_deployment_steps_sequence", "\"Sequence\" > 0");
                    table.CheckConstraint("ck_deployment_steps_state", "\"State\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_deployment_steps_deployment_operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "deployment_operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deployment_steps_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_deployment_device_plans_DeviceId",
                table: "deployment_device_plans",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "uq_deployment_device_plans_plan_device",
                table: "deployment_device_plans",
                columns: new[] { "PlanId", "DeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_deployment_device_states_DeviceId",
                table: "deployment_device_states",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_deployment_locks_DeploymentId",
                table: "deployment_locks",
                column: "DeploymentId");

            migrationBuilder.CreateIndex(
                name: "uq_deployment_locks_node",
                table: "deployment_locks",
                column: "NodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_deployment_operations_plan",
                table: "deployment_operations",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "uq_deployment_operations_node_nonterminal",
                table: "deployment_operations",
                column: "NodeId",
                unique: true,
                filter: "\"State\" NOT IN (9, 12, 13, 14, 15, 16, 17)");

            migrationBuilder.CreateIndex(
                name: "ix_deployment_plans_node",
                table: "deployment_plans",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_deployment_steps_DeviceId",
                table: "deployment_steps",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "uq_deployment_steps_operation_sequence",
                table: "deployment_steps",
                columns: new[] { "OperationId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deployment_device_plans");

            migrationBuilder.DropTable(
                name: "deployment_device_states");

            migrationBuilder.DropTable(
                name: "deployment_locks");

            migrationBuilder.DropTable(
                name: "deployment_steps");

            migrationBuilder.DropTable(
                name: "deployment_operations");

            migrationBuilder.DropTable(
                name: "deployment_plans");
        }
    }
}
