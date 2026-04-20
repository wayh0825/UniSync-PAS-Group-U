using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniSync.Core.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGradingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcademicGrade",
                table: "ResearchSubmissions");

            migrationBuilder.DropColumn(
                name: "IsGradeFinalized",
                table: "ResearchSubmissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcademicGrade",
                table: "ResearchSubmissions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGradeFinalized",
                table: "ResearchSubmissions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
