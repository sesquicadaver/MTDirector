using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mfc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResponseAssessmentQualitySchemaM7305 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Confidence",
                table: "response_assessments",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "VisibilityStatus",
                table: "response_assessments",
                type: "integer",
                nullable: false,
                defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Confidence",
                table: "response_assessments");

            migrationBuilder.DropColumn(
                name: "VisibilityStatus",
                table: "response_assessments");
        }
    }
}
