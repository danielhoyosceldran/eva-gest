using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EvaGest.Migrations
{
    /// <inheritdoc />
    public partial class AddCashMovementStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "cash_movements",
                type: "TEXT",
                nullable: false,
                // Every movement that already exists was live: none had been voided,
                // because until now a removed movement was deleted outright.
                defaultValue: "Active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "status",
                table: "cash_movements");
        }
    }
}
