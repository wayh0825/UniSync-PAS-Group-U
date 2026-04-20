using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniSync.Core.Migrations
{
    /// <inheritdoc />
    public partial class Phase3Governance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.CreateTable(
                name: "ReallocationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResearchSubmissionId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResponseNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedById = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReallocationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReallocationRequests_AspNetUsers_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReallocationRequests_ResearchSubmissions_ResearchSubmissionId",
                        column: x => x.ResearchSubmissionId,
                        principalTable: "ResearchSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReallocationRequests_RequestedById",
                table: "ReallocationRequests",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_ReallocationRequests_ResearchSubmissionId",
                table: "ReallocationRequests",
                column: "ResearchSubmissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReallocationRequests");

            migrationBuilder.DropColumn(
                name: "AcademicGrade",
                table: "ResearchSubmissions");

            migrationBuilder.DropColumn(
                name: "IsGradeFinalized",
                table: "ResearchSubmissions");
        }
    }
}
