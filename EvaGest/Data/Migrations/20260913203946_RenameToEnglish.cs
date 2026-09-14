using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EvaGest.Migrations
{
    /// <summary>
    /// Renames every table, column and index from Catalan to English, and rewrites the
    /// text values that are really identifiers: the enum members stored as text and the
    /// keys of the settings table.
    ///
    /// Written by hand as renames. Scaffolding produced a drop-and-recreate instead,
    /// because both the CLR type names and the table names changed at once and EF could
    /// not pair them up, and that would have emptied the one database this app has.
    ///
    /// Primary-key, foreign-key and check-constraint NAMES stay Catalan: SQLite keeps
    /// them inside the table's DDL text and can only change them by rebuilding the whole
    /// table. They are never referenced from code, so the rebuild is not worth the risk.
    /// </summary>
    /// <inheritdoc />
    public partial class RenameToEnglish : Migration
    {

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // tables
            migrationBuilder.RenameTable(name: "cites", newName: "appointments");
            migrationBuilder.RenameTable(name: "configuracio", newName: "settings");
            migrationBuilder.RenameTable(name: "dies_tancats", newName: "closed_days");
            migrationBuilder.RenameTable(name: "horari_barberia", newName: "shop_schedule");
            migrationBuilder.RenameTable(name: "horaris_treballadora", newName: "worker_schedules");
            migrationBuilder.RenameTable(name: "metodes_pagament", newName: "payment_methods");
            migrationBuilder.RenameTable(name: "moviments_caixa", newName: "cash_movements");
            migrationBuilder.RenameTable(name: "productes", newName: "products");
            migrationBuilder.RenameTable(name: "serveis", newName: "services");
            migrationBuilder.RenameTable(name: "treballadores", newName: "workers");
            migrationBuilder.RenameTable(name: "vendes", newName: "sales");
            migrationBuilder.RenameTable(name: "venda_desglossaments", newName: "sale_breakdowns");
            migrationBuilder.RenameTable(name: "venda_linies", newName: "sale_lines");

            // columns
            migrationBuilder.RenameColumn(name: "data", table: "appointments", newName: "date");
            migrationBuilder.RenameColumn(name: "durada_min", table: "appointments", newName: "duration_min");
            migrationBuilder.RenameColumn(name: "estat", table: "appointments", newName: "status");
            migrationBuilder.RenameColumn(name: "hora", table: "appointments", newName: "time");
            migrationBuilder.RenameColumn(name: "nom_convidat", table: "appointments", newName: "guest_name");
            migrationBuilder.RenameColumn(name: "observacions", table: "appointments", newName: "notes");
            migrationBuilder.RenameColumn(name: "servei_id", table: "appointments", newName: "service_id");
            migrationBuilder.RenameColumn(name: "telefon_convidat", table: "appointments", newName: "guest_phone");
            migrationBuilder.RenameColumn(name: "treballadora_id", table: "appointments", newName: "worker_id");
            migrationBuilder.RenameColumn(name: "adormit", table: "clients", newName: "asleep");
            migrationBuilder.RenameColumn(name: "correu", table: "clients", newName: "email");
            migrationBuilder.RenameColumn(name: "data_naixement", table: "clients", newName: "birth_date");
            migrationBuilder.RenameColumn(name: "mobil", table: "clients", newName: "mobile");
            migrationBuilder.RenameColumn(name: "nom", table: "clients", newName: "name");
            migrationBuilder.RenameColumn(name: "observacions", table: "clients", newName: "notes");
            migrationBuilder.RenameColumn(name: "clau", table: "settings", newName: "key");
            migrationBuilder.RenameColumn(name: "valor", table: "settings", newName: "value");
            migrationBuilder.RenameColumn(name: "data", table: "closed_days", newName: "date");
            migrationBuilder.RenameColumn(name: "motiu", table: "closed_days", newName: "reason");
            migrationBuilder.RenameColumn(name: "dia_setmana", table: "shop_schedule", newName: "weekday");
            migrationBuilder.RenameColumn(name: "hora_obertura", table: "shop_schedule", newName: "opening_time");
            migrationBuilder.RenameColumn(name: "hora_tancament", table: "shop_schedule", newName: "closing_time");
            migrationBuilder.RenameColumn(name: "dia_setmana", table: "worker_schedules", newName: "weekday");
            migrationBuilder.RenameColumn(name: "hora_fi", table: "worker_schedules", newName: "end_time");
            migrationBuilder.RenameColumn(name: "hora_inici", table: "worker_schedules", newName: "start_time");
            migrationBuilder.RenameColumn(name: "treballadora_id", table: "worker_schedules", newName: "worker_id");
            migrationBuilder.RenameColumn(name: "actiu", table: "payment_methods", newName: "active");
            migrationBuilder.RenameColumn(name: "nom", table: "payment_methods", newName: "name");
            migrationBuilder.RenameColumn(name: "concepte", table: "cash_movements", newName: "concept");
            migrationBuilder.RenameColumn(name: "data", table: "cash_movements", newName: "date");
            migrationBuilder.RenameColumn(name: "import_cents", table: "cash_movements", newName: "amount_cents");
            migrationBuilder.RenameColumn(name: "iva_bp", table: "cash_movements", newName: "vat_bp");
            migrationBuilder.RenameColumn(name: "iva_cents", table: "cash_movements", newName: "vat_cents");
            migrationBuilder.RenameColumn(name: "metode_pagament_id", table: "cash_movements", newName: "payment_method_id");
            migrationBuilder.RenameColumn(name: "observacions", table: "cash_movements", newName: "notes");
            migrationBuilder.RenameColumn(name: "tipus", table: "cash_movements", newName: "type");
            migrationBuilder.RenameColumn(name: "actiu", table: "products", newName: "active");
            migrationBuilder.RenameColumn(name: "categoria", table: "products", newName: "category");
            migrationBuilder.RenameColumn(name: "iva_bp", table: "products", newName: "vat_bp");
            migrationBuilder.RenameColumn(name: "nom", table: "products", newName: "name");
            migrationBuilder.RenameColumn(name: "preu_cents", table: "products", newName: "price_cents");
            migrationBuilder.RenameColumn(name: "actiu", table: "services", newName: "active");
            migrationBuilder.RenameColumn(name: "durada_min", table: "services", newName: "duration_min");
            migrationBuilder.RenameColumn(name: "iva_bp", table: "services", newName: "vat_bp");
            migrationBuilder.RenameColumn(name: "nom", table: "services", newName: "name");
            migrationBuilder.RenameColumn(name: "preu_cents", table: "services", newName: "price_cents");
            migrationBuilder.RenameColumn(name: "actiu", table: "workers", newName: "active");
            migrationBuilder.RenameColumn(name: "nom", table: "workers", newName: "name");
            migrationBuilder.RenameColumn(name: "cita_id", table: "sales", newName: "appointment_id");
            migrationBuilder.RenameColumn(name: "data", table: "sales", newName: "date");
            migrationBuilder.RenameColumn(name: "estat", table: "sales", newName: "status");
            migrationBuilder.RenameColumn(name: "hora", table: "sales", newName: "time");
            migrationBuilder.RenameColumn(name: "iva_cents", table: "sales", newName: "vat_cents");
            migrationBuilder.RenameColumn(name: "iva_mode", table: "sales", newName: "vat_mode");
            migrationBuilder.RenameColumn(name: "metode_pagament_id", table: "sales", newName: "payment_method_id");
            migrationBuilder.RenameColumn(name: "nom_convidat", table: "sales", newName: "guest_name");
            migrationBuilder.RenameColumn(name: "observacions", table: "sales", newName: "notes");
            migrationBuilder.RenameColumn(name: "telefon_convidat", table: "sales", newName: "guest_phone");
            migrationBuilder.RenameColumn(name: "treballadora_id", table: "sales", newName: "worker_id");
            migrationBuilder.RenameColumn(name: "iva_bp", table: "sale_breakdowns", newName: "vat_bp");
            migrationBuilder.RenameColumn(name: "iva_cents", table: "sale_breakdowns", newName: "vat_cents");
            migrationBuilder.RenameColumn(name: "venda_id", table: "sale_breakdowns", newName: "sale_id");
            migrationBuilder.RenameColumn(name: "descripcio", table: "sale_lines", newName: "description");
            migrationBuilder.RenameColumn(name: "import_cents", table: "sale_lines", newName: "amount_cents");
            migrationBuilder.RenameColumn(name: "iva_bp", table: "sale_lines", newName: "vat_bp");
            migrationBuilder.RenameColumn(name: "preu_unitari_cents", table: "sale_lines", newName: "unit_price_cents");
            migrationBuilder.RenameColumn(name: "producte_id", table: "sale_lines", newName: "product_id");
            migrationBuilder.RenameColumn(name: "quantitat", table: "sale_lines", newName: "quantity");
            migrationBuilder.RenameColumn(name: "servei_id", table: "sale_lines", newName: "service_id");
            migrationBuilder.RenameColumn(name: "venda_id", table: "sale_lines", newName: "sale_id");

            // indexes
            migrationBuilder.RenameIndex(name: "ix_cites_client_id", table: "appointments", newName: "ix_appointments_client_id");
            migrationBuilder.RenameIndex(name: "ix_cites_servei_id", table: "appointments", newName: "ix_appointments_service_id");
            migrationBuilder.RenameIndex(name: "ix_cites_treballadora_id", table: "appointments", newName: "ix_appointments_worker_id");
            migrationBuilder.RenameIndex(name: "ix_cites_data_hora", table: "appointments", newName: "ix_appointments_date_time");
            migrationBuilder.RenameIndex(name: "ix_dies_tancats_data", table: "closed_days", newName: "ix_closed_days_date");
            migrationBuilder.RenameIndex(name: "ix_horaris_treballadora_treballadora_id", table: "worker_schedules", newName: "ix_worker_schedules_worker_id");
            migrationBuilder.RenameIndex(name: "ix_metodes_pagament_nom", table: "payment_methods", newName: "ix_payment_methods_name");
            migrationBuilder.RenameIndex(name: "ix_moviments_caixa_data", table: "cash_movements", newName: "ix_cash_movements_date");
            migrationBuilder.RenameIndex(name: "ix_moviments_caixa_metode_pagament_id", table: "cash_movements", newName: "ix_cash_movements_payment_method_id");
            migrationBuilder.RenameIndex(name: "ix_vendes_cita_id", table: "sales", newName: "ix_sales_appointment_id");
            migrationBuilder.RenameIndex(name: "ix_vendes_client_id", table: "sales", newName: "ix_sales_client_id");
            migrationBuilder.RenameIndex(name: "ix_vendes_data", table: "sales", newName: "ix_sales_date");
            migrationBuilder.RenameIndex(name: "ix_vendes_estat", table: "sales", newName: "ix_sales_status");
            migrationBuilder.RenameIndex(name: "ix_vendes_metode_pagament_id", table: "sales", newName: "ix_sales_payment_method_id");
            migrationBuilder.RenameIndex(name: "ix_vendes_treballadora_id", table: "sales", newName: "ix_sales_worker_id");
            migrationBuilder.RenameIndex(name: "ix_venda_desglossaments_venda_id", table: "sale_breakdowns", newName: "ix_sale_breakdowns_sale_id");
            migrationBuilder.RenameIndex(name: "ix_venda_linies_producte_id", table: "sale_lines", newName: "ix_sale_lines_product_id");
            migrationBuilder.RenameIndex(name: "ix_venda_linies_servei_id", table: "sale_lines", newName: "ix_sale_lines_service_id");
            migrationBuilder.RenameIndex(name: "ix_venda_linies_venda_id", table: "sale_lines", newName: "ix_sale_lines_sale_id");

            // enum members are stored as text, so the rows carry the old names
            migrationBuilder.Sql("UPDATE appointments SET status = 'Pending' WHERE status = 'Pendent';");
            migrationBuilder.Sql("UPDATE appointments SET status = 'Completed' WHERE status = 'Realitzada';");
            migrationBuilder.Sql("UPDATE appointments SET status = 'Cancelled' WHERE status = 'Cancellada';");
            migrationBuilder.Sql("UPDATE appointments SET status = 'NoShow' WHERE status = 'NoAssistida';");
            migrationBuilder.Sql("UPDATE sales SET status = 'Active' WHERE status = 'Activa';");
            migrationBuilder.Sql("UPDATE sales SET status = 'Voided' WHERE status = 'Anullada';");
            migrationBuilder.Sql("UPDATE sales SET vat_mode = 'Included' WHERE vat_mode = 'Inclos';");
            migrationBuilder.Sql("UPDATE sales SET vat_mode = 'NotIncluded' WHERE vat_mode = 'NoInclos';");
            migrationBuilder.Sql("UPDATE cash_movements SET type = 'In' WHERE type = 'Entrada';");
            migrationBuilder.Sql("UPDATE cash_movements SET type = 'Out' WHERE type = 'Sortida';");
            migrationBuilder.Sql("UPDATE worker_schedules SET weekday = 'Mon' WHERE weekday = 'Dl';");
            migrationBuilder.Sql("UPDATE worker_schedules SET weekday = 'Tue' WHERE weekday = 'Dt';");
            migrationBuilder.Sql("UPDATE worker_schedules SET weekday = 'Wed' WHERE weekday = 'Dc';");
            migrationBuilder.Sql("UPDATE worker_schedules SET weekday = 'Thu' WHERE weekday = 'Dj';");
            migrationBuilder.Sql("UPDATE worker_schedules SET weekday = 'Fri' WHERE weekday = 'Dv';");
            migrationBuilder.Sql("UPDATE worker_schedules SET weekday = 'Sat' WHERE weekday = 'Ds';");
            migrationBuilder.Sql("UPDATE worker_schedules SET weekday = 'Sun' WHERE weekday = 'Dg';");
            migrationBuilder.Sql("UPDATE shop_schedule SET weekday = 'Mon' WHERE weekday = 'Dl';");
            migrationBuilder.Sql("UPDATE shop_schedule SET weekday = 'Tue' WHERE weekday = 'Dt';");
            migrationBuilder.Sql("UPDATE shop_schedule SET weekday = 'Wed' WHERE weekday = 'Dc';");
            migrationBuilder.Sql("UPDATE shop_schedule SET weekday = 'Thu' WHERE weekday = 'Dj';");
            migrationBuilder.Sql("UPDATE shop_schedule SET weekday = 'Fri' WHERE weekday = 'Dv';");
            migrationBuilder.Sql("UPDATE shop_schedule SET weekday = 'Sat' WHERE weekday = 'Ds';");
            migrationBuilder.Sql("UPDATE shop_schedule SET weekday = 'Sun' WHERE weekday = 'Dg';");

            // settings rows are addressed by key
            migrationBuilder.Sql("UPDATE settings SET key = 'shop_name' WHERE key = 'barberia_nom';");
            migrationBuilder.Sql("UPDATE settings SET key = 'shop_address' WHERE key = 'barberia_adreca';");
            migrationBuilder.Sql("UPDATE settings SET key = 'shop_phone' WHERE key = 'barberia_telefon';");
            migrationBuilder.Sql("UPDATE settings SET key = 'default_vat_bp' WHERE key = 'iva_bp_defecte';");
            migrationBuilder.Sql("UPDATE settings SET key = 'vat_mode' WHERE key = 'iva_mode';");
            migrationBuilder.Sql("UPDATE settings SET key = 'apply_vat_to_till' WHERE key = 'aplicar_iva_caixa';");
            migrationBuilder.Sql("UPDATE settings SET key = 'default_appointment_duration_min' WHERE key = 'durada_defecte_cita_min';");
            migrationBuilder.Sql("UPDATE settings SET key = 'backup_time' WHERE key = 'hora_backup';");
            migrationBuilder.Sql("UPDATE settings SET key = 'backups_to_keep' WHERE key = 'backups_a_conservar';");
            migrationBuilder.Sql("UPDATE settings SET key = 'last_automatic_backup' WHERE key = 'ultima_copia_automatica';");
            migrationBuilder.Sql("UPDATE settings SET key = 'show_guest_notice' WHERE key = 'mostrar_avis_convidat';");
            migrationBuilder.Sql("UPDATE settings SET key = 'confirmation_sound' WHERE key = 'so_confirmacio';");
            migrationBuilder.Sql("UPDATE settings SET key = 'agenda_slot_minutes' WHERE key = 'minuts_slot_agenda';");
            migrationBuilder.Sql("UPDATE settings SET value = 'Included' WHERE key = 'vat_mode' AND value = 'Inclos';");
            migrationBuilder.Sql("UPDATE settings SET value = 'NotIncluded' WHERE key = 'vat_mode' AND value = 'NoInclos';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // enum members are stored as text, so the rows carry the old names
            migrationBuilder.Sql("UPDATE appointments SET status = 'Pendent' WHERE status = 'Pending';");
            migrationBuilder.Sql("UPDATE appointments SET status = 'Realitzada' WHERE status = 'Completed';");
            migrationBuilder.Sql("UPDATE appointments SET status = 'Cancellada' WHERE status = 'Cancelled';");
            migrationBuilder.Sql("UPDATE appointments SET status = 'NoAssistida' WHERE status = 'NoShow';");
            migrationBuilder.Sql("UPDATE sales SET status = 'Activa' WHERE status = 'Active';");
            migrationBuilder.Sql("UPDATE sales SET status = 'Anullada' WHERE status = 'Voided';");
            migrationBuilder.Sql("UPDATE sales SET vat_mode = 'Inclos' WHERE vat_mode = 'Included';");
            migrationBuilder.Sql("UPDATE sales SET vat_mode = 'NoInclos' WHERE vat_mode = 'NotIncluded';");
            migrationBuilder.Sql("UPDATE cash_movements SET type = 'Entrada' WHERE type = 'In';");
            migrationBuilder.Sql("UPDATE cash_movements SET type = 'Sortida' WHERE type = 'Out';");
            migrationBuilder.Sql("UPDATE worker_schedules SET weekday = 'Dl' WHERE weekday = 'Mon';");
            migrationBuilder.Sql("UPDATE worker_schedules SET weekday = 'Dt' WHERE weekday = 'Tue';");
            migrationBuilder.Sql("UPDATE worker_schedules SET weekday = 'Dc' WHERE weekday = 'Wed';");
            migrationBuilder.Sql("UPDATE worker_schedules SET weekday = 'Dj' WHERE weekday = 'Thu';");
            migrationBuilder.Sql("UPDATE worker_schedules SET weekday = 'Dv' WHERE weekday = 'Fri';");
            migrationBuilder.Sql("UPDATE worker_schedules SET weekday = 'Ds' WHERE weekday = 'Sat';");
            migrationBuilder.Sql("UPDATE worker_schedules SET weekday = 'Dg' WHERE weekday = 'Sun';");
            migrationBuilder.Sql("UPDATE shop_schedule SET weekday = 'Dl' WHERE weekday = 'Mon';");
            migrationBuilder.Sql("UPDATE shop_schedule SET weekday = 'Dt' WHERE weekday = 'Tue';");
            migrationBuilder.Sql("UPDATE shop_schedule SET weekday = 'Dc' WHERE weekday = 'Wed';");
            migrationBuilder.Sql("UPDATE shop_schedule SET weekday = 'Dj' WHERE weekday = 'Thu';");
            migrationBuilder.Sql("UPDATE shop_schedule SET weekday = 'Dv' WHERE weekday = 'Fri';");
            migrationBuilder.Sql("UPDATE shop_schedule SET weekday = 'Ds' WHERE weekday = 'Sat';");
            migrationBuilder.Sql("UPDATE shop_schedule SET weekday = 'Dg' WHERE weekday = 'Sun';");

            // settings rows are addressed by key
            migrationBuilder.Sql("UPDATE settings SET key = 'barberia_nom' WHERE key = 'shop_name';");
            migrationBuilder.Sql("UPDATE settings SET key = 'barberia_adreca' WHERE key = 'shop_address';");
            migrationBuilder.Sql("UPDATE settings SET key = 'barberia_telefon' WHERE key = 'shop_phone';");
            migrationBuilder.Sql("UPDATE settings SET key = 'iva_bp_defecte' WHERE key = 'default_vat_bp';");
            migrationBuilder.Sql("UPDATE settings SET key = 'iva_mode' WHERE key = 'vat_mode';");
            migrationBuilder.Sql("UPDATE settings SET key = 'aplicar_iva_caixa' WHERE key = 'apply_vat_to_till';");
            migrationBuilder.Sql("UPDATE settings SET key = 'durada_defecte_cita_min' WHERE key = 'default_appointment_duration_min';");
            migrationBuilder.Sql("UPDATE settings SET key = 'hora_backup' WHERE key = 'backup_time';");
            migrationBuilder.Sql("UPDATE settings SET key = 'backups_a_conservar' WHERE key = 'backups_to_keep';");
            migrationBuilder.Sql("UPDATE settings SET key = 'ultima_copia_automatica' WHERE key = 'last_automatic_backup';");
            migrationBuilder.Sql("UPDATE settings SET key = 'mostrar_avis_convidat' WHERE key = 'show_guest_notice';");
            migrationBuilder.Sql("UPDATE settings SET key = 'so_confirmacio' WHERE key = 'confirmation_sound';");
            migrationBuilder.Sql("UPDATE settings SET key = 'minuts_slot_agenda' WHERE key = 'agenda_slot_minutes';");
            migrationBuilder.Sql("UPDATE settings SET value = 'Inclos' WHERE key = 'iva_mode' AND value = 'Included';");
            migrationBuilder.Sql("UPDATE settings SET value = 'NoInclos' WHERE key = 'iva_mode' AND value = 'NotIncluded';");

            // indexes
            migrationBuilder.RenameIndex(name: "ix_appointments_client_id", table: "appointments", newName: "ix_cites_client_id");
            migrationBuilder.RenameIndex(name: "ix_appointments_service_id", table: "appointments", newName: "ix_cites_servei_id");
            migrationBuilder.RenameIndex(name: "ix_appointments_worker_id", table: "appointments", newName: "ix_cites_treballadora_id");
            migrationBuilder.RenameIndex(name: "ix_appointments_date_time", table: "appointments", newName: "ix_cites_data_hora");
            migrationBuilder.RenameIndex(name: "ix_closed_days_date", table: "closed_days", newName: "ix_dies_tancats_data");
            migrationBuilder.RenameIndex(name: "ix_worker_schedules_worker_id", table: "worker_schedules", newName: "ix_horaris_treballadora_treballadora_id");
            migrationBuilder.RenameIndex(name: "ix_payment_methods_name", table: "payment_methods", newName: "ix_metodes_pagament_nom");
            migrationBuilder.RenameIndex(name: "ix_cash_movements_date", table: "cash_movements", newName: "ix_moviments_caixa_data");
            migrationBuilder.RenameIndex(name: "ix_cash_movements_payment_method_id", table: "cash_movements", newName: "ix_moviments_caixa_metode_pagament_id");
            migrationBuilder.RenameIndex(name: "ix_sales_appointment_id", table: "sales", newName: "ix_vendes_cita_id");
            migrationBuilder.RenameIndex(name: "ix_sales_client_id", table: "sales", newName: "ix_vendes_client_id");
            migrationBuilder.RenameIndex(name: "ix_sales_date", table: "sales", newName: "ix_vendes_data");
            migrationBuilder.RenameIndex(name: "ix_sales_status", table: "sales", newName: "ix_vendes_estat");
            migrationBuilder.RenameIndex(name: "ix_sales_payment_method_id", table: "sales", newName: "ix_vendes_metode_pagament_id");
            migrationBuilder.RenameIndex(name: "ix_sales_worker_id", table: "sales", newName: "ix_vendes_treballadora_id");
            migrationBuilder.RenameIndex(name: "ix_sale_breakdowns_sale_id", table: "sale_breakdowns", newName: "ix_venda_desglossaments_venda_id");
            migrationBuilder.RenameIndex(name: "ix_sale_lines_product_id", table: "sale_lines", newName: "ix_venda_linies_producte_id");
            migrationBuilder.RenameIndex(name: "ix_sale_lines_service_id", table: "sale_lines", newName: "ix_venda_linies_servei_id");
            migrationBuilder.RenameIndex(name: "ix_sale_lines_sale_id", table: "sale_lines", newName: "ix_venda_linies_venda_id");

            // columns
            migrationBuilder.RenameColumn(name: "date", table: "appointments", newName: "data");
            migrationBuilder.RenameColumn(name: "duration_min", table: "appointments", newName: "durada_min");
            migrationBuilder.RenameColumn(name: "status", table: "appointments", newName: "estat");
            migrationBuilder.RenameColumn(name: "time", table: "appointments", newName: "hora");
            migrationBuilder.RenameColumn(name: "guest_name", table: "appointments", newName: "nom_convidat");
            migrationBuilder.RenameColumn(name: "notes", table: "appointments", newName: "observacions");
            migrationBuilder.RenameColumn(name: "service_id", table: "appointments", newName: "servei_id");
            migrationBuilder.RenameColumn(name: "guest_phone", table: "appointments", newName: "telefon_convidat");
            migrationBuilder.RenameColumn(name: "worker_id", table: "appointments", newName: "treballadora_id");
            migrationBuilder.RenameColumn(name: "asleep", table: "clients", newName: "adormit");
            migrationBuilder.RenameColumn(name: "email", table: "clients", newName: "correu");
            migrationBuilder.RenameColumn(name: "birth_date", table: "clients", newName: "data_naixement");
            migrationBuilder.RenameColumn(name: "mobile", table: "clients", newName: "mobil");
            migrationBuilder.RenameColumn(name: "name", table: "clients", newName: "nom");
            migrationBuilder.RenameColumn(name: "notes", table: "clients", newName: "observacions");
            migrationBuilder.RenameColumn(name: "key", table: "settings", newName: "clau");
            migrationBuilder.RenameColumn(name: "value", table: "settings", newName: "valor");
            migrationBuilder.RenameColumn(name: "date", table: "closed_days", newName: "data");
            migrationBuilder.RenameColumn(name: "reason", table: "closed_days", newName: "motiu");
            migrationBuilder.RenameColumn(name: "weekday", table: "shop_schedule", newName: "dia_setmana");
            migrationBuilder.RenameColumn(name: "opening_time", table: "shop_schedule", newName: "hora_obertura");
            migrationBuilder.RenameColumn(name: "closing_time", table: "shop_schedule", newName: "hora_tancament");
            migrationBuilder.RenameColumn(name: "weekday", table: "worker_schedules", newName: "dia_setmana");
            migrationBuilder.RenameColumn(name: "end_time", table: "worker_schedules", newName: "hora_fi");
            migrationBuilder.RenameColumn(name: "start_time", table: "worker_schedules", newName: "hora_inici");
            migrationBuilder.RenameColumn(name: "worker_id", table: "worker_schedules", newName: "treballadora_id");
            migrationBuilder.RenameColumn(name: "active", table: "payment_methods", newName: "actiu");
            migrationBuilder.RenameColumn(name: "name", table: "payment_methods", newName: "nom");
            migrationBuilder.RenameColumn(name: "concept", table: "cash_movements", newName: "concepte");
            migrationBuilder.RenameColumn(name: "date", table: "cash_movements", newName: "data");
            migrationBuilder.RenameColumn(name: "amount_cents", table: "cash_movements", newName: "import_cents");
            migrationBuilder.RenameColumn(name: "vat_bp", table: "cash_movements", newName: "iva_bp");
            migrationBuilder.RenameColumn(name: "vat_cents", table: "cash_movements", newName: "iva_cents");
            migrationBuilder.RenameColumn(name: "payment_method_id", table: "cash_movements", newName: "metode_pagament_id");
            migrationBuilder.RenameColumn(name: "notes", table: "cash_movements", newName: "observacions");
            migrationBuilder.RenameColumn(name: "type", table: "cash_movements", newName: "tipus");
            migrationBuilder.RenameColumn(name: "active", table: "products", newName: "actiu");
            migrationBuilder.RenameColumn(name: "category", table: "products", newName: "categoria");
            migrationBuilder.RenameColumn(name: "vat_bp", table: "products", newName: "iva_bp");
            migrationBuilder.RenameColumn(name: "name", table: "products", newName: "nom");
            migrationBuilder.RenameColumn(name: "price_cents", table: "products", newName: "preu_cents");
            migrationBuilder.RenameColumn(name: "active", table: "services", newName: "actiu");
            migrationBuilder.RenameColumn(name: "duration_min", table: "services", newName: "durada_min");
            migrationBuilder.RenameColumn(name: "vat_bp", table: "services", newName: "iva_bp");
            migrationBuilder.RenameColumn(name: "name", table: "services", newName: "nom");
            migrationBuilder.RenameColumn(name: "price_cents", table: "services", newName: "preu_cents");
            migrationBuilder.RenameColumn(name: "active", table: "workers", newName: "actiu");
            migrationBuilder.RenameColumn(name: "name", table: "workers", newName: "nom");
            migrationBuilder.RenameColumn(name: "appointment_id", table: "sales", newName: "cita_id");
            migrationBuilder.RenameColumn(name: "date", table: "sales", newName: "data");
            migrationBuilder.RenameColumn(name: "status", table: "sales", newName: "estat");
            migrationBuilder.RenameColumn(name: "time", table: "sales", newName: "hora");
            migrationBuilder.RenameColumn(name: "vat_cents", table: "sales", newName: "iva_cents");
            migrationBuilder.RenameColumn(name: "vat_mode", table: "sales", newName: "iva_mode");
            migrationBuilder.RenameColumn(name: "payment_method_id", table: "sales", newName: "metode_pagament_id");
            migrationBuilder.RenameColumn(name: "guest_name", table: "sales", newName: "nom_convidat");
            migrationBuilder.RenameColumn(name: "notes", table: "sales", newName: "observacions");
            migrationBuilder.RenameColumn(name: "guest_phone", table: "sales", newName: "telefon_convidat");
            migrationBuilder.RenameColumn(name: "worker_id", table: "sales", newName: "treballadora_id");
            migrationBuilder.RenameColumn(name: "vat_bp", table: "sale_breakdowns", newName: "iva_bp");
            migrationBuilder.RenameColumn(name: "vat_cents", table: "sale_breakdowns", newName: "iva_cents");
            migrationBuilder.RenameColumn(name: "sale_id", table: "sale_breakdowns", newName: "venda_id");
            migrationBuilder.RenameColumn(name: "description", table: "sale_lines", newName: "descripcio");
            migrationBuilder.RenameColumn(name: "amount_cents", table: "sale_lines", newName: "import_cents");
            migrationBuilder.RenameColumn(name: "vat_bp", table: "sale_lines", newName: "iva_bp");
            migrationBuilder.RenameColumn(name: "unit_price_cents", table: "sale_lines", newName: "preu_unitari_cents");
            migrationBuilder.RenameColumn(name: "product_id", table: "sale_lines", newName: "producte_id");
            migrationBuilder.RenameColumn(name: "quantity", table: "sale_lines", newName: "quantitat");
            migrationBuilder.RenameColumn(name: "service_id", table: "sale_lines", newName: "servei_id");
            migrationBuilder.RenameColumn(name: "sale_id", table: "sale_lines", newName: "venda_id");

            // tables
            migrationBuilder.RenameTable(name: "appointments", newName: "cites");
            migrationBuilder.RenameTable(name: "settings", newName: "configuracio");
            migrationBuilder.RenameTable(name: "closed_days", newName: "dies_tancats");
            migrationBuilder.RenameTable(name: "shop_schedule", newName: "horari_barberia");
            migrationBuilder.RenameTable(name: "worker_schedules", newName: "horaris_treballadora");
            migrationBuilder.RenameTable(name: "payment_methods", newName: "metodes_pagament");
            migrationBuilder.RenameTable(name: "cash_movements", newName: "moviments_caixa");
            migrationBuilder.RenameTable(name: "products", newName: "productes");
            migrationBuilder.RenameTable(name: "services", newName: "serveis");
            migrationBuilder.RenameTable(name: "workers", newName: "treballadores");
            migrationBuilder.RenameTable(name: "sales", newName: "vendes");
            migrationBuilder.RenameTable(name: "sale_breakdowns", newName: "venda_desglossaments");
            migrationBuilder.RenameTable(name: "sale_lines", newName: "venda_linies");
        }
    }
}
