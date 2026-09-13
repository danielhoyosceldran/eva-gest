# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

EvaGest: a Windows desktop app (WPF, .NET 9, SQLite) for running a barbershop —
appointments, clients, sales, till, reports. Fully local, single machine, no
internet. One non-technical user.

The full specification is in [`docs/plan/`](docs/plan/), written before the code.
Code comments cite it by code: `RF-10` (functional requirement), `CU-01` (use
case), `disseny-ui 9` / `pantalles 1` (section of that document).
**[`docs/README.md`](docs/README.md) maps every code to its document — read it
before changing anything those comments point at.**

## Commands

```powershell
dotnet build EvaGest.slnx
dotnet test  EvaGest.slnx
dotnet test  EvaGest.slnx --filter "FullyQualifiedName~IvaCalculator"

# migrations: run from the EvaGest/ project folder
dotnet ef migrations add <Name>
```

The build is warning-free. Keep it that way. All 339 tests run in about a second
(test execution itself — `dotnet test` including build/restore takes longer), so
run the whole suite rather than guessing which part is affected.

The app itself needs a Windows desktop session; it cannot be launched from a
headless agent run. Verify through tests instead.

## Language

- Identifiers, namespaces, file names, database columns and UI strings: **Catalan**
  (`Treballadora`, `Venda`, `Cita`, `Graella`).
- Comments, commit messages and these docs: **English**.
- Test names: Catalan, `What_Condition_ExpectedResult`
  (`Cita_cancellada_no_compta_al_solapament`).

Keep both conventions. Do not translate domain terms into English.

## Architecture

```
Views (XAML)  →  ViewModels  →  Services  →  BarberiaDbContext  →  SQLite
                      ↘                ↙
                  pure calculators (IvaCalculator, Diners, Indicadors, GraellaHelper)
```

Hard rules, from `docs/plan/capa-mvvm.md` §1:

- Views know nothing but their ViewModel, through bindings.
- **ViewModels never see EF Core.** No `DbContext`, no `Include()`, no LINQ over
  `DbSet`.
- **Services never touch the UI.** No dialog, no control, no `Brush`.
- Pure calculators are static, stateless and dependency-free.
- There is deliberately **no repository layer** — services are the boundary.

Folders: `Models/` `Data/` `Services/` `ViewModels/{Pagines,Dialegs,Elements}/`
`Views/{Pagines,Dialegs,Elements}/` `Helpers/` `Resources/`.

## Money and VAT — the part that must not be broken

These five rules come from `requeriments-barberia-v2.md` §6.3–6.4 and are
restated with context in `docs/README.md`:

1. Money is `int` **cents**; VAT rates are `int` **basis points** (`2100` = 21 %).
   Never `decimal`/`float`/`double`. Suffixes `_cents`/`_bp` in SQL,
   `Cents`/`Bp` in C#.
2. Each sale line **freezes** the VAT rate applied at the time. Editing the
   catalogue must never change a past sale.
3. A sale's breakdown is computed **per VAT-rate group**, not per line — summing
   per-line bases loses a cent against the total.
4. **Canonical aggregation rule:** a period's totals are the sum of the values
   already stored on each `Venda`. Never recompute them from `VendaLinies`.
   `CaixaService` and `InformesService` may read lines only to count units and
   classify service vs. product.
5. Rounding is `MidpointRounding.AwayFromZero`, never C#'s default banker's
   rounding.

Voided sales are never deleted: `Estat = Anullada`, and every balance, VAT and
ranking query filters on `Estat = Activa`.

Touching anything money-related means running the tests before calling it done.

## Async and UI thread

- Every service method is `async`, using EF Core's `...Async` methods.
- No `.Result`, no `.Wait()`.
- Page load goes in a `Carregar()` method called by the host, not in a
  constructor.
- Each page ViewModel exposes `Carregant` while it queries.
- ViewModels must stay off the UI thread: no `Dispatcher`, `Brush` or
  `Application.Current` inside one — the tests build them off-thread. Timers and
  visual-tree work belong in the view's code-behind.

## Conventions worth knowing before editing

- **Dialog ViewModels are constructed with `new`, not resolved from DI** (about
  23 call sites). They take their host's services plus the entity being edited.
  Do not "fix" this by registering them in `App.Configurar`.
- Page ViewModels *are* in DI, transient; `MainWindowViewModel` is a singleton
  holding one instance of each page so navigation keeps state.
- MVVM plumbing is CommunityToolkit.Mvvm source generators: `[ObservableProperty]`
  on a `_camelCase` field, `[RelayCommand]` on a method. Bind to `PascalCase` /
  `XxxCommand`.
- Grid geometry lives in `Helpers/GraellaHelper.cs` as pure statics, in whole
  minutes from midnight. Never use `TimeOnly.AddMinutes` for grid maths — it
  wraps past midnight silently.
- Styles come from `Resources/{Colors,Typography,Metrics,Controls}.xaml`. Use the
  existing tokens (`BotoPrimari`, `TargetaDada`, `PadPagina`…); do not hardcode
  colours or sizes in a view.
- `Helpers/*Converter.cs` are binding converters; check whether one already
  exists before adding another.

## UI wording

From `disseny-ui.md` §9 — user-visible text is design, not decoration:

- Concrete verbs on buttons (`Cobrar`, `Fer còpia ara`), never `Acceptar`.
- The same noun through a whole flow.
- Errors say what to do: "Cal indicar el nom del client per guardar la cita.",
  not "Error de validació".
- No apologies, no technical terms, no exception text in front of the user.
- Empty states invite an action instead of showing a blank table.

## Tests

`EvaGest.Tests/` — xUnit v3 + AwesomeAssertions. Organised as `Calculs/`,
`Serveis/`, `Vistes/`, `Escenaris/`, plus `Infra/` for shared fixtures (fake
services, test-data builders, the WPF app fixture, in-memory DB setup). The
catalogue of blocks is in `docs/plan/pla-proves.md`.

- `Serveis/` and `Escenaris/` run against a real SQLite in-memory database (not
  the EF InMemory provider).
- `Vistes/` are WPF smoke/layout tests — they parse real XAML resource
  dictionaries and run real Arrange-pass layout math via the `AplicacioWpf`
  fixture (`[Collection(ColleccioWpf.Nom)]`); this is why the test project sets
  `UseWPF=true`.

Add a test for anything touching money, availability/overlap, client keys or the
weekly grid.

## Bug tracking

Every bug found gets a row in `bugs.csv` at the repo root: short description,
repro steps, and whether it's solved (and, if so, the commit that fixed it).
Add the row when the bug is found; fill in the commit once it's fixed.

## Planned refactor (not started)

Two changes are planned but **not yet underway** — don't rename or restructure
anything toward this unprompted, only when explicitly asked to work on it:

- **Code to English.** Identifiers, namespaces, file names and comments move to
  English; only user-facing strings stay localized. This supersedes the
  Catalan-identifiers rule above once the refactor actually starts — until
  then, keep writing new code the current Catalan-identifiers way.
- **Switchable UI language**, Catalan and Spanish. V1 is restart-based: read a
  language setting at startup and load the matching resource dictionary — no
  dynamic runtime swap needed yet.
- **Comment the code well**, for readability — this project overrides the usual
  "comment only the non-obvious" default; once underway, add comments explaining
  what non-trivial code does, not just why.

## Scope

Explicitly out of scope, per the requirements §5: online booking, client
notifications, online payments, external integrations, multi-currency,
per-user roles, payroll. Do not add them. (Internationalisation was listed
here too but is now a planned refactor — see above.)
