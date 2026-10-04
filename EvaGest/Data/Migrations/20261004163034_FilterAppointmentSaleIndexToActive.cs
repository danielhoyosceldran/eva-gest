using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EvaGest.Migrations
{
    /// <inheritdoc />
    public partial class FilterAppointmentSaleIndexToActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sales_appointment_id",
                table: "sales");

            migrationBuilder.CreateIndex(
                name: "ix_sales_appointment_id",
                table: "sales",
                column: "appointment_id",
                unique: true,
                filter: "status = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sales_appointment_id",
                table: "sales");

            migrationBuilder.CreateIndex(
                name: "ix_sales_appointment_id",
                table: "sales",
                column: "appointment_id",
                unique: true);
        }
    }
}
