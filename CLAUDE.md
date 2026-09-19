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
dotnet test  EvaGest.slnx --filter "FullyQualifiedName~VatCalculator"

# migrations: run from the EvaGest/ project folder
dotnet ef migrations add <Name>
```

The build is warning-free. Keep it that way. All 581 tests run in about two seconds
(test execution itself — `dotnet test` including build/restore takes longer), so
run the whole suite rather than guessing which part is affected.

If `dotnet test` appears to hang for minutes, it is not the tests: they exit in
seconds. Look for a stale `testhost` process holding the output DLL
(`Get-Process testhost | Stop-Process -Force`), the app still running and locking
`EvaGest.exe`, or Visual Studio's test discovery holding the same files.

The app itself needs a Windows desktop session; it cannot be launched from a
headless agent run. Verify through tests instead.

## Language

- Identifiers, namespaces, file names, database columns, comments, commit
  messages, test names and these docs: **English** (`Worker`, `Sale`,
  `Appointment`, `Grid`).
- Everything the user reads: **Catalan or Spanish**, never a literal in code.
  Button labels, error messages, empty states, the FAQ and the CSV headers the
  accountant gets all come from `Resources/Texts.resx` (Catalan, the neutral
  table) and `Resources/Texts.es.resx` (Spanish).
- Test names read as English sentences, `What_Condition_ExpectedResult`
  (`A_cancelled_appointment_does_not_count`).

The spec in `docs/plan/` is Catalan and older than this rename;
[`docs/README.md`](docs/README.md) has the term-by-term mapping.

### Adding a user-visible string

1. Add the row to **both** `.resx` files, keyed in English.
2. Add the matching property to `Resources/Texts.cs`:
   `public static string Key => Get(nameof(Key));`, and its name to `Keys`.
3. Use it as `{x:Static t:Texts.Key}` in XAML (`xmlns:t="clr-namespace:EvaGest.Resources"`)
   or `Texts.Key` in a ViewModel. Messages that take values are
   `string.Format(Texts.Key, …)` with `{0}`-style placeholders.

`LanguageTests` fails if either table is missing the key or if the two disagree
about how many placeholders the string takes.

The language is read once at startup (`AppLanguage.Use`, from
`ConfigKeys.Language`) and fixed for the session — the settings page says a
restart is needed. `AppLanguage.Culture` is what formats every amount and date;
do not hardcode a culture.

## Architecture

```
Views (XAML)  →  ViewModels  →  Services  →  ShopDbContext  →  SQLite
                      ↘                ↙
                  pure calculators (VatCalculator, Money, Indicators, GridHelper)
```

Hard rules, from `docs/plan/capa-mvvm.md` §1:

- Views know nothing but their ViewModel, through bindings.
- **ViewModels never see EF Core.** No `DbContext`, no `Include()`, no LINQ over
  `DbSet`.
- **Services never touch the UI.** No dialog, no control, no `Brush`.
- Pure calculators are static, stateless and dependency-free.
- There is deliberately **no repository layer** — services are the boundary.

Folders: `Models/` `Data/` `Services/` `ViewModels/{Pages,Dialogs,Elements}/`
`Views/{Pages,Dialogs,Elements}/` `Helpers/` `Resources/`.

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
   already stored on each `Sale`. Never recompute them from `SaleLines`.
   `TillService` and `ReportsService` may read lines only to count units and
   classify service vs. product.
5. Rounding is `MidpointRounding.AwayFromZero`, never C#'s default banker's
   rounding.

Voided sales are never deleted: `Status = Voided`, and every balance, VAT and
ranking query filters on `Status = Active`.

Touching anything money-related means running the tests before calling it done.

## Async and UI thread

- Every service method is `async`, using EF Core's `...Async` methods.
- No `.Result`, no `.Wait()`.
- Page load goes in a `Load()` method called by the host, not in a
  constructor.
- Each page ViewModel exposes `Loading` while it queries.
- ViewModels must stay off the UI thread: no `Dispatcher`, `Brush` or
  `Application.Current` inside one — the tests build them off-thread. Timers and
  visual-tree work belong in the view's code-behind.

## Error handling and logging

Logging used to exist only in `App.xaml.cs` (app start/stop, DB-open failure,
unhandled exceptions). It is being filled in incrementally rather than all at
once — **every change that touches a code path capable of failing must do a
short self-check before it's done: is this path's success/failure logged, and
does the user find out when it fails? If either is missing, close that gap in
the same change**, don't file it away for later. This is how coverage grows to
the whole app over time instead of staying a one-off pass.

Convention, established in `Services/SaleService.cs` and `App.xaml.cs`:

- `Serilog.Log` (static, configured once in `App.xaml.cs`) is used directly
  in Services and `App.xaml.cs`. There is no injected `ILogger` — no DI
  logging abstraction, by design (small app, one file sink).
- `Log.Information` — a business event completed (a sale edited/voided, an
  appointment cancelled, a backup taken). Include the entity id.
- `Log.Warning` — a failure that was caught and safely worked around. Never
  swallow an exception with an empty `catch` — see `SoundService.PlayConfirmation`
  for the pattern: still don't surface it to the user if it genuinely
  shouldn't block them, but always leave a record.
- `Log.Error` — a failure that reached the user as an error message.
- `Log.Fatal` — a failure that stops the app or the process (unreadable
  database, an exception that escaped to `AppDomain.UnhandledException`).
- ViewModels generally don't call `Log`. Expected/validatable failures aren't
  exceptions — they're `Error*` properties shown in the view (UI wording
  rules below apply). Most real exceptions are left to bubble to the global
  `DispatcherUnhandledException` handler in `App.xaml.cs`, which already logs
  and shows a message. A ViewModel calls `Log` only if it itself catches.
- `docs/plan/casos-us.md` (and `docs/README.md` for the code mapping)
  sometimes states explicitly that an action must be logged (e.g. CU-04, sale
  edit/void) — treat that as a requirement, not a suggestion, when touching
  that flow.

## Conventions worth knowing before editing

- **Dialog ViewModels are constructed with `new`, not resolved from DI** (about
  23 call sites). They take their host's services plus the entity being edited.
  Do not "fix" this by registering them in `App.Configure`.
- Page ViewModels *are* in DI, transient; `MainWindowViewModel` is a singleton
  holding one instance of each page so navigation keeps state.
- MVVM plumbing is CommunityToolkit.Mvvm source generators: `[ObservableProperty]`
  on a `_camelCase` field, `[RelayCommand]` on a method. Bind to `PascalCase` /
  `XxxCommand`.
- Grid geometry lives in `Helpers/GridHelper.cs` as pure statics, in whole
  minutes from midnight. Never use `TimeOnly.AddMinutes` for grid maths — it
  wraps past midnight silently.
- Styles come from `Resources/{Colors,Typography,Metrics,Controls}.xaml`. Use the
  existing tokens (`PrimaryButton`, `DataCard`, `PagePad`…); do not hardcode
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
- Both languages say the same thing in the same voice. Translate the intent,
  not the words.

## Tests

`EvaGest.Tests/` — xUnit v3 + AwesomeAssertions. Organised as `Calculations/`,
`Services/`, `Views/`, `Scenarios/`, plus `Infra/` for shared fixtures (fake
services, test-data builders, the WPF app fixture, in-memory DB setup). The
catalogue of blocks is in `docs/plan/pla-proves.md`; block V, the interface
language, is newer than that document.

- `Services/` and `Scenarios/` run against a real SQLite in-memory database (not
  the EF InMemory provider).
- `Views/` are WPF smoke/layout tests — they parse real XAML resource
  dictionaries and run real Arrange-pass layout math via the `ApplicationWpf`
  fixture (`[Collection(WpfCollection.Name)]`); this is why the test project sets
  `UseWPF=true`.

Add a test for anything touching money, availability/overlap, client keys or the
weekly grid.

## Bug tracking

Every bug found gets a row in `bugs.csv` at the repo root: short description,
repro steps, and whether it's solved (and, if so, the commit that fixed it).
Add the row when the bug is found; fill in the commit once it's fixed.

## Planned refactor

- **Code to English** — **done**. Identifiers, namespaces, file names, comments,
  test names, tables and columns are English; only what the user reads stays
  Catalan. Migration `RenameToEnglish` carries an existing database across.
- **Switchable UI language**, Catalan and Spanish — **done**, restart-based.
  See the Language section above. A live swap without restarting is still not
  needed; don't build one unprompted.
- **Comment the code well**, for readability — this project overrides the usual
  "comment only the non-obvious" default; once underway, add comments explaining
  what non-trivial code does, not just why.

## Scope

Explicitly out of scope, per the requirements §5: online booking, client
notifications, online payments, external integrations, multi-currency,
per-user roles, payroll. Do not add them. (Internationalisation was listed
here too but is now a planned refactor — see above.)
