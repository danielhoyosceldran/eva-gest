# EvaGest — documentation index

EvaGest is a local Windows desktop app (WPF + SQLite) for running a barbershop:
appointments, clients, sales, till and reports. Single machine, no internet, no
online booking. The interface is in Catalan or Spanish, chosen in the settings;
the code that builds it is in English.

The specification lives in [`plan/`](plan/). Those documents were written before
the code and are the source of truth for *what* the app must do and *why* a
decision was taken. The code comments refer to them by code (`RF-10`, `CU-01`,
`disseny-ui 9`, `pantalles 1`) — this page is the map from those codes back to
the document that defines them.

The spec documents are written in Catalan. This index and `CLAUDE.md` are in
English, to match the code comments.

**The code is English, the spec is Catalan.** The identifiers were renamed from
Catalan to English after the spec was written, so a document saying `Venda` is
describing the class now called `Sale`. The mapping is the obvious one — the
main terms are collected below.

| Spec | Code | Spec | Code |
|---|---|---|---|
| Cita | `Appointment` | Caixa | `Till` |
| Venda | `Sale` | Catàleg | `Catalog` |
| VendaLinia | `SaleLine` | Informes | `Reports` |
| VendaDesglossament | `SaleBreakdown` | Configuració | `Settings` |
| Treballadora | `Worker` | Graella | `Grid` |
| Servei | `Service` | Disponibilitat | `Availability` |
| Producte | `Product` | IVA | `Vat` |
| MetodePagament | `PaymentMethod` | Diners | `Money` |
| MovimentCaixa | `CashMovement` | Inici | `Home` |
| HorariBarberia | `ShopSchedule` | Fitxa | `Record` |
| HorariTreballadora | `WorkerSchedule` | Avís | `Notice` |
| DiaTancat | `ClosedDay` | Franja | `Interval` |

Database tables and columns follow the same rename (`vendes` → `sales`,
`nom_convidat` → `guest_name`), as do the enum members stored as text
(`Pendent` → `Pending`, `Dl` → `Mon`) and the keys of the settings table.
Migration `RenameToEnglish` moves an existing database across.

---

## Where to look

| Question | Document |
|---|---|
| What must the app do? (`RF-xx`) | [`plan/requeriments-barberia-v2.md`](plan/requeriments-barberia-v2.md) |
| How does a flow run step by step? (`CU-xx`) | [`plan/casos-us.md`](plan/casos-us.md) |
| Which layer may call which, service by service | [`plan/capa-mvvm.md`](plan/capa-mvvm.md) |
| Tables, columns, indexes, `ON DELETE` behaviour | [`plan/esquema-bbdd.md`](plan/esquema-bbdd.md) · [`plan/diagrama-er.mermaid`](plan/diagrama-er.mermaid) |
| C# entities, enums, money conversion, VAT maths | [`plan/models-domini.md`](plan/models-domini.md) |
| What each screen and dialog contains | [`plan/pantalles.md`](plan/pantalles.md) |
| Colours, typography, metrics, wording of the UI | [`plan/disseny-ui.md`](plan/disseny-ui.md) |
| Stack and layering overview | [`plan/stack-arquitectura.md`](plan/stack-arquitectura.md) |
| What is tested and how tests are named | [`plan/pla-proves.md`](plan/pla-proves.md) |
| Build order the project followed | [`plan/pla-desenvolupament.md`](plan/pla-desenvolupament.md) |
| Project/solution setup, `.csproj`, migrations | [`plan/setup-projecte.md`](plan/setup-projecte.md) |
| What was promised to the client, in plain words | [`plan/propuesta-cliente-barberia.md`](plan/propuesta-cliente-barberia.md) · [`plan/resumen-rapido-clienta.md`](plan/resumen-rapido-clienta.md) |

---

## Reference codes used in code comments

### `RF-xx` — functional requirements

Defined in [`plan/requeriments-barberia-v2.md`](plan/requeriments-barberia-v2.md), section 3.

| Code | Subject |
|---|---|
| RF-01 | Home screen (day's appointments, sales, balance, quick buttons) |
| RF-02 | Agenda: weekly view, appointment states, overlap warning |
| RF-03 | Clients: create, edit, search, birthday notice |
| RF-04 | Delete vs. "sleep" a client |
| RF-05 | Client record (visits, spend, frequency, history) |
| RF-05bis | Guest clients (name only, no record kept) |
| RF-06 | Workers: weekly schedule, active/inactive, overlap check |
| RF-07 | Services (price, own VAT rate, duration) |
| RF-08 | Products (price, category, own VAT rate) |
| RF-09 | Sales: standalone flow and from-appointment flow |
| RF-10 | Editing and voiding a sale (never deleted) |
| RF-11 | Quick sale |
| RF-12 | Configurable payment methods |
| RF-13 | Till movements in/out |
| RF-14 | Till balance |
| RF-15 | Sales history |
| RF-16 | Reports and analysis |
| RF-17 | Indicator rules (averages, frequency, percentages) |
| RF-18 | Client history |
| RF-19 | Built-in FAQ help |
| RF-20 | Plain-language error messages |
| RF-21 | Confirmation of destructive actions |
| RF-22 | Confirmation sound |
| RF-23 | Settings (opening hours, slot size, VAT mode, defaults) |
| RF-24 | Automatic backup |
| RF-25 | Manual backup |
| RF-26 | Restoring a backup |
| RF-27 | CSV export for the accountant (modelo 303) |
| RF-28 | Basic logging, 30 days retained |

### `CU-xx` — use cases

Defined in [`plan/casos-us.md`](plan/casos-us.md), with a flowchart each.

| Code | Subject |
|---|---|
| CU-01 | Create an appointment (CU-01b: the overlap calculation) |
| CU-02 | Set an appointment's state |
| CU-03 | Register a sale (CU-03b: the VAT breakdown) |
| CU-04 | Edit or void a sale |
| CU-05 | Delete or sleep a client |
| CU-06 | Register a till movement |
| CU-07 | Consult reports per worker |
| CU-08 | Export for the accountant |
| CU-09 | Manual backup / restore |
| CU-10 | Change the VAT mode |
| CU-11 | Automatic backup |
| CU-12 | Application startup (single instance, unreadable database, migrations, seed) |

### Other references

| Reference | Meaning |
|---|---|
| `disseny-ui N` | Section N of [`plan/disseny-ui.md`](plan/disseny-ui.md). Section 9 ("Veu de la interfície") is the wording rulebook cited most often |
| `pantalles N` | Section N of [`plan/pantalles.md`](plan/pantalles.md) |
| `RNF-xx` | Non-functional requirements, section 4 of the requirements document |
| `6.x` | Low-level technical decisions, section 6 of the requirements document — 6.3 money, 6.4 VAT, 6.7 availability, 6.8 export |

---

## The five decisions that explain most of the code

These are spread across the spec; they are collected here because nearly every
change to money, reports or the agenda touches one of them.

1. **Money is `int` cents, VAT rates are `int` basis points.** `1500` = 15,00 €,
   `2100` = 21,00 %. Never `decimal`, `float` or `double`. Columns end in
   `_cents` and `_bp`, properties in `Cents` and `Bp`. *(requirements 6.3)*

2. **VAT is frozen per transaction.** Each sale line copies the rate that
   applied at the time. Changing the catalogue never changes a past sale.
   *(requirements 6.4)*

3. **A sale's breakdown is computed per VAT rate group, not per line.** Summing
   per-line bases loses a cent against the total. *(requirements 6.4, CU-03b)*

4. **Canonical aggregation rule: a period's totals are always the sum of the
   values already stored on each sale — never recomputed from the lines.** The
   two methods diverge, and the gap grows without bound. `TillService` and
   `ReportsService` may read `SaleLines` only to count units and classify
   services vs. products, never to recompute fiscal amounts. *(requirements 6.4)*

5. **Rounding is away-from-zero, never banker's.** `Math.Round(x,
   MidpointRounding.AwayFromZero)`. *(requirements 6.4)*

---

## Deviations from the spec, as built

Recorded here so they are not read as bugs:

- **Target framework is `net9.0-windows`**, not the .NET 10 named in the
  requirements. Reason is in a comment in `EvaGest/EvaGest.csproj`.
- **No repository layer.** `capa-mvvm.md` §2 already revises the earlier
  `stack-arquitectura.md` proposal: services are the boundary, they use
  `DbContext` directly.
- **Assertions use AwesomeAssertions**, the maintained fork, not
  FluentAssertions — see `pla-proves.md` §2.
- **Code, schema and test names are English**, while the spec is Catalan — see
  the table at the top of this page. Only what the user reads is translated.
- **The interface has two languages**, Catalan and Spanish, where the spec
  named Catalan only and ruled internationalisation out of scope. Both come
  from `EvaGest/Resources/Texts*.resx`; the choice is `ConfigKeys.Language`,
  read once at startup, so changing it asks for a restart.
- **`ClientKey` is the whole name and nothing else** — decision 6.1 originally
  made it the *first* name plus the last nine phone digits, and RF-03 described
  the duplicate check as a warning that never blocks. Both were revised in place
  in `requeriments-barberia-v2.md`: a name now identifies a client, two clients
  may not share one even on different numbers, and the dialog refuses the save
  instead of warning. Two clients with different names may share a phone, which
  the old key rejected. The original shape could not work — a detection
  heuristic wants false positives, the unique index on the column tolerates
  none, and the index won by throwing `DbUpdateException` at the user.
- **Constraint names in the database stay Catalan** (`pk_cites`,
  `ck_cites_client_xor`, `fk_vendes_clients_client_id`). SQLite keeps them
  inside the table's DDL text, so renaming them means rebuilding every table;
  nothing in the code refers to them.
