using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EvaGest.Migrations
{
    /// <inheritdoc />
    public partial class KeepRecordsWhenPaymentMethodDeleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_cash_movements_payment_methods_payment_method_id",
                table: "cash_movements");

            migrationBuilder.DropForeignKey(
                name: "fk_sales_payment_methods_payment_method_id",
                table: "sales");

            migrationBuilder.AlterColumn<int>(
                name: "payment_method_id",
                table: "sales",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "payment_method_id",
                table: "cash_movements",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "fk_cash_movements_payment_methods_payment_method_id",
                table: "cash_movements",
                column: "payment_method_id",
                principalTable: "payment_methods",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_sales_payment_methods_payment_method_id",
                table: "sales",
                column: "payment_method_id",
                principalTable: "payment_methods",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_cash_movements_payment_methods_payment_method_id",
                table: "cash_movements");

            migrationBuilder.DropForeignKey(
                name: "fk_sales_payment_methods_payment_method_id",
                table: "sales");

            migrationBuilder.AlterColumn<int>(
                name: "payment_method_id",
                table: "sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "payment_method_id",
                table: "cash_movements",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_cash_movements_payment_methods_payment_method_id",
                table: "cash_movements",
                column: "payment_method_id",
                principalTable: "payment_methods",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_sales_payment_methods_payment_method_id",
                table: "sales",
                column: "payment_method_id",
                principalTable: "payment_methods",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
