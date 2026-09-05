using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EvaGest.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    client_key = table.Column<string>(type: "TEXT", nullable: false),
                    nom = table.Column<string>(type: "TEXT", nullable: false),
                    mobil = table.Column<string>(type: "TEXT", nullable: false),
                    correu = table.Column<string>(type: "TEXT", nullable: true),
                    data_naixement = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    observacions = table.Column<string>(type: "TEXT", nullable: true),
                    adormit = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "configuracio",
                columns: table => new
                {
                    clau = table.Column<string>(type: "TEXT", nullable: false),
                    valor = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracio", x => x.clau);
                });

            migrationBuilder.CreateTable(
                name: "dies_tancats",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    motiu = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dies_tancats", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "horari_barberia",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    dia_setmana = table.Column<string>(type: "TEXT", nullable: false),
                    hora_obertura = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    hora_tancament = table.Column<TimeOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_horari_barberia", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "metodes_pagament",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    nom = table.Column<string>(type: "TEXT", nullable: false),
                    actiu = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_metodes_pagament", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productes",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    nom = table.Column<string>(type: "TEXT", nullable: false),
                    preu_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    categoria = table.Column<string>(type: "TEXT", nullable: true),
                    iva_bp = table.Column<int>(type: "INTEGER", nullable: false),
                    actiu = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_productes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "serveis",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    nom = table.Column<string>(type: "TEXT", nullable: false),
                    preu_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    iva_bp = table.Column<int>(type: "INTEGER", nullable: false),
                    durada_min = table.Column<int>(type: "INTEGER", nullable: true),
                    actiu = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_serveis", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "treballadores",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    nom = table.Column<string>(type: "TEXT", nullable: false),
                    actiu = table.Column<bool>(type: "INTEGER", nullable: false),
                    color = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_treballadores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "moviments_caixa",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    tipus = table.Column<string>(type: "TEXT", nullable: false),
                    import_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    base_cents = table.Column<int>(type: "INTEGER", nullable: true),
                    iva_cents = table.Column<int>(type: "INTEGER", nullable: true),
                    iva_bp = table.Column<int>(type: "INTEGER", nullable: true),
                    metode_pagament_id = table.Column<int>(type: "INTEGER", nullable: false),
                    concepte = table.Column<string>(type: "TEXT", nullable: false),
                    observacions = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moviments_caixa", x => x.id);
                    table.ForeignKey(
                        name: "fk_moviments_caixa_metodes_pagament_metode_pagament_id",
                        column: x => x.metode_pagament_id,
                        principalTable: "metodes_pagament",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cites",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    hora = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    durada_min = table.Column<int>(type: "INTEGER", nullable: false),
                    client_id = table.Column<int>(type: "INTEGER", nullable: true),
                    nom_convidat = table.Column<string>(type: "TEXT", nullable: true),
                    telefon_convidat = table.Column<string>(type: "TEXT", nullable: true),
                    servei_id = table.Column<int>(type: "INTEGER", nullable: true),
                    treballadora_id = table.Column<int>(type: "INTEGER", nullable: true),
                    estat = table.Column<string>(type: "TEXT", nullable: false),
                    observacions = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cites", x => x.id);
                    table.CheckConstraint("ck_cites_client_xor", "(client_id IS NOT NULL AND nom_convidat IS NULL) OR (client_id IS NULL AND nom_convidat IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_cites_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cites_serveis_servei_id",
                        column: x => x.servei_id,
                        principalTable: "serveis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_cites_treballadores_treballadora_id",
                        column: x => x.treballadora_id,
                        principalTable: "treballadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "horaris_treballadora",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    treballadora_id = table.Column<int>(type: "INTEGER", nullable: false),
                    dia_setmana = table.Column<string>(type: "TEXT", nullable: false),
                    hora_inici = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    hora_fi = table.Column<TimeOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_horaris_treballadora", x => x.id);
                    table.ForeignKey(
                        name: "fk_horaris_treballadora_treballadores_treballadora_id",
                        column: x => x.treballadora_id,
                        principalTable: "treballadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vendes",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    hora = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    client_id = table.Column<int>(type: "INTEGER", nullable: true),
                    nom_convidat = table.Column<string>(type: "TEXT", nullable: true),
                    telefon_convidat = table.Column<string>(type: "TEXT", nullable: true),
                    cita_id = table.Column<int>(type: "INTEGER", nullable: true),
                    treballadora_id = table.Column<int>(type: "INTEGER", nullable: true),
                    metode_pagament_id = table.Column<int>(type: "INTEGER", nullable: false),
                    base_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    iva_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    total_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    iva_mode = table.Column<string>(type: "TEXT", nullable: false),
                    estat = table.Column<string>(type: "TEXT", nullable: false),
                    observacions = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vendes", x => x.id);
                    table.CheckConstraint("ck_vendes_client_xor", "(client_id IS NOT NULL AND nom_convidat IS NULL) OR (client_id IS NULL AND nom_convidat IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_vendes_cites_cita_id",
                        column: x => x.cita_id,
                        principalTable: "cites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_vendes_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vendes_metodes_pagament_metode_pagament_id",
                        column: x => x.metode_pagament_id,
                        principalTable: "metodes_pagament",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vendes_treballadores_treballadora_id",
                        column: x => x.treballadora_id,
                        principalTable: "treballadores",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "venda_desglossaments",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    venda_id = table.Column<int>(type: "INTEGER", nullable: false),
                    iva_bp = table.Column<int>(type: "INTEGER", nullable: false),
                    base_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    iva_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    total_cents = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_venda_desglossaments", x => x.id);
                    table.ForeignKey(
                        name: "fk_venda_desglossaments_vendes_venda_id",
                        column: x => x.venda_id,
                        principalTable: "vendes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "venda_linies",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    venda_id = table.Column<int>(type: "INTEGER", nullable: false),
                    servei_id = table.Column<int>(type: "INTEGER", nullable: true),
                    producte_id = table.Column<int>(type: "INTEGER", nullable: true),
                    descripcio = table.Column<string>(type: "TEXT", nullable: false),
                    quantitat = table.Column<int>(type: "INTEGER", nullable: false),
                    preu_unitari_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    iva_bp = table.Column<int>(type: "INTEGER", nullable: false),
                    import_cents = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_venda_linies", x => x.id);
                    table.ForeignKey(
                        name: "fk_venda_linies_productes_producte_id",
                        column: x => x.producte_id,
                        principalTable: "productes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_venda_linies_serveis_servei_id",
                        column: x => x.servei_id,
                        principalTable: "serveis",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_venda_linies_vendes_venda_id",
                        column: x => x.venda_id,
                        principalTable: "vendes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cites_client_id",
                table: "cites",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_cites_data_hora",
                table: "cites",
                columns: new[] { "data", "hora" });

            migrationBuilder.CreateIndex(
                name: "ix_cites_servei_id",
                table: "cites",
                column: "servei_id");

            migrationBuilder.CreateIndex(
                name: "ix_cites_treballadora_id",
                table: "cites",
                column: "treballadora_id");

            migrationBuilder.CreateIndex(
                name: "ix_clients_client_key",
                table: "clients",
                column: "client_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dies_tancats_data",
                table: "dies_tancats",
                column: "data",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_horaris_treballadora_treballadora_id",
                table: "horaris_treballadora",
                column: "treballadora_id");

            migrationBuilder.CreateIndex(
                name: "ix_metodes_pagament_nom",
                table: "metodes_pagament",
                column: "nom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_moviments_caixa_data",
                table: "moviments_caixa",
                column: "data");

            migrationBuilder.CreateIndex(
                name: "ix_moviments_caixa_metode_pagament_id",
                table: "moviments_caixa",
                column: "metode_pagament_id");

            migrationBuilder.CreateIndex(
                name: "ix_venda_desglossaments_venda_id",
                table: "venda_desglossaments",
                column: "venda_id");

            migrationBuilder.CreateIndex(
                name: "ix_venda_linies_producte_id",
                table: "venda_linies",
                column: "producte_id");

            migrationBuilder.CreateIndex(
                name: "ix_venda_linies_servei_id",
                table: "venda_linies",
                column: "servei_id");

            migrationBuilder.CreateIndex(
                name: "ix_venda_linies_venda_id",
                table: "venda_linies",
                column: "venda_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendes_cita_id",
                table: "vendes",
                column: "cita_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vendes_client_id",
                table: "vendes",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendes_data",
                table: "vendes",
                column: "data");

            migrationBuilder.CreateIndex(
                name: "ix_vendes_estat",
                table: "vendes",
                column: "estat");

            migrationBuilder.CreateIndex(
                name: "ix_vendes_metode_pagament_id",
                table: "vendes",
                column: "metode_pagament_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendes_treballadora_id",
                table: "vendes",
                column: "treballadora_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracio");

            migrationBuilder.DropTable(
                name: "dies_tancats");

            migrationBuilder.DropTable(
                name: "horari_barberia");

            migrationBuilder.DropTable(
                name: "horaris_treballadora");

            migrationBuilder.DropTable(
                name: "moviments_caixa");

            migrationBuilder.DropTable(
                name: "venda_desglossaments");

            migrationBuilder.DropTable(
                name: "venda_linies");

            migrationBuilder.DropTable(
                name: "productes");

            migrationBuilder.DropTable(
                name: "vendes");

            migrationBuilder.DropTable(
                name: "cites");

            migrationBuilder.DropTable(
                name: "metodes_pagament");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "serveis");

            migrationBuilder.DropTable(
                name: "treballadores");
        }
    }
}
