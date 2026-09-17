using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EvaGest.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseCategoryAndCashMovementLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "category_id",
                table: "cash_movements",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "worker_id",
                table: "cash_movements",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "expense_categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    active = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_categories", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cash_movements_category_id",
                table: "cash_movements",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_movements_worker_id",
                table: "cash_movements",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_categories_name",
                table: "expense_categories",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_cash_movements_expense_categories_category_id",
                table: "cash_movements",
                column: "category_id",
                principalTable: "expense_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_cash_movements_workers_worker_id",
                table: "cash_movements",
                column: "worker_id",
                principalTable: "workers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_cash_movements_expense_categories_category_id",
                table: "cash_movements");

            migrationBuilder.DropForeignKey(
                name: "fk_cash_movements_workers_worker_id",
                table: "cash_movements");

            migrationBuilder.DropTable(
                name: "expense_categories");

            migrationBuilder.DropIndex(
                name: "ix_cash_movements_category_id",
                table: "cash_movements");

            migrationBuilder.DropIndex(
                name: "ix_cash_movements_worker_id",
                table: "cash_movements");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "cash_movements");

            migrationBuilder.DropColumn(
                name: "worker_id",
                table: "cash_movements");
        }
    }
}
