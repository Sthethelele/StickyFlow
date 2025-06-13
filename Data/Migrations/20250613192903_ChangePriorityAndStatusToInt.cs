using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StickyFlow.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangePriorityAndStatusToInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Weight",
                table: "Priorities");

            migrationBuilder.RenameColumn(
                name: "PriorityId",
                table: "Priorities",
                newName: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Priorities",
                newName: "PriorityId");

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                table: "Priorities",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
