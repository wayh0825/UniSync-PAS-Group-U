using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniSync.Core.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceGroupProtocol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "SubmissionGroupMembers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "SubmissionGroupMembers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionGroupMembers_UserId",
                table: "SubmissionGroupMembers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionGroupMembers_AspNetUsers_UserId",
                table: "SubmissionGroupMembers",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionGroupMembers_AspNetUsers_UserId",
                table: "SubmissionGroupMembers");

            migrationBuilder.DropIndex(
                name: "IX_SubmissionGroupMembers_UserId",
                table: "SubmissionGroupMembers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SubmissionGroupMembers");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "SubmissionGroupMembers");
        }
    }
}
