using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FilterArtifactsSchemaM307 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "filter_artifacts",
                columns: table => new
                {
                    ResourceHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ArtifactId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhysicalSemanticsHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CompilerProfileHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    LogicalEffectivePolicyHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    DeviceResolvedPolicyHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    AnalysisBundleHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CapabilityHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CompilerVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CompiledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Compression = table.Column<short>(type: "smallint", nullable: false),
                    UncompressedSize = table.Column<long>(type: "bigint", nullable: false),
                    CompressedPayload = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_filter_artifacts", x => x.ResourceHash);
                    table.CheckConstraint("ck_filter_artifact_analysis_bundle_hash", "octet_length(\"AnalysisBundleHash\") = 32");
                    table.CheckConstraint("ck_filter_artifact_capability_hash", "octet_length(\"CapabilityHash\") = 32");
                    table.CheckConstraint("ck_filter_artifact_compiler_profile_hash", "octet_length(\"CompilerProfileHash\") = 32");
                    table.CheckConstraint("ck_filter_artifact_device_resolved_hash", "octet_length(\"DeviceResolvedPolicyHash\") = 32");
                    table.CheckConstraint("ck_filter_artifact_id", "char_length(\"ArtifactId\") = 16");
                    table.CheckConstraint("ck_filter_artifact_logical_effective_hash", "octet_length(\"LogicalEffectivePolicyHash\") = 32");
                    table.CheckConstraint("ck_filter_artifact_physical_semantics_hash", "octet_length(\"PhysicalSemanticsHash\") = 32");
                    table.CheckConstraint("ck_filter_artifact_resource_hash", "octet_length(\"ResourceHash\") = 32");
                    table.CheckConstraint("ck_filter_artifact_size", "\"UncompressedSize\" > 0 AND \"UncompressedSize\" <= 33554432");
                });

            migrationBuilder.CreateIndex(
                name: "IX_filter_artifacts_ArtifactId",
                table: "filter_artifacts",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_filter_artifacts_DeviceId",
                table: "filter_artifacts",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "filter_artifacts");
        }
    }
}
