using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EvaGest.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    entity = table.Column<string>(type: "TEXT", nullable: false),
                    entity_id = table.Column<int>(type: "INTEGER", nullable: false),
                    action = table.Column<string>(type: "TEXT", nullable: false),
                    before = table.Column<string>(type: "TEXT", nullable: true),
                    after = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_entity_entity_id",
                table: "audit_entries",
                columns: new[] { "entity", "entity_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entries");
        }
    }
}
