using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniSync.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddSupervisorCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxSupervisionCapacity",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxSupervisionCapacity",
                table: "AspNetUsers");
        }
    }
}
