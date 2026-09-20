# Audit prompt

Paste the block below verbatim as a single message to start a deep code-quality and
bug-hunting pass over this repository.

It is deliberately specific to EvaGest. A generic "review my code" prompt returns generic
findings; this one encodes the invariants this codebase actually rests on, and the bug
classes that have already bitten it more than once, so the audit hunts recurrences instead
of rediscovering style opinions.

Keep it current: when a new bug class shows up in `bugs.csv`, add it to section 3.

---

## The prompt

> Audit this repository for **code quality and live bugs**. Read `CLAUDE.md` first, then
> `bugs.csv`, then work from the actual code. Do not rely on your memory of this project.
>
> ### 0. Ground yourself before judging anything
>
> Run these and report the real numbers, not the ones any document claims:
>
> ```powershell
> dotnet build EvaGest.slnx -t:Rebuild
> dotnet test EvaGest.slnx
> git log --oneline -15
> git status --short
> ```
>
> A clean rebuild must be **warning-free** and the suite must be **fully green**. If either
> is not, that is finding number one and everything else waits. Note that incremental
> builds can report stale warnings — only a `-t:Rebuild` count is real.
>
> Then map the system before critiquing it: projects, folders, entry points, the flow from
> View to ViewModel to Service to `ShopDbContext`, which services are singletons vs
> transient, what `MainWindowViewModel` holds, and where state lives across navigation.
>
> ### 1. Where the bugs in this codebase actually are
>
> The service layer is strong and well tested. **Nearly every row in `bugs.csv` is a value
> corrupted while crossing a seam** — View↔ViewModel or ViewModel↔Service — where the
> service was correct about a value the caller handed it wrong. Spend your effort there.
>
> For each of the flows below, trace the value end to end and state what reaches the
> database and what reaches the screen:
>
> - charging a new sale, charging from an appointment, **editing an existing sale**
> - creating, editing and deleting a cash movement
> - creating and editing an appointment, and each status change
> - the till summary and the per-rate VAT table for a period
> - the quarterly CSV export
> - every page's filter controls and their reloads
> - backup, restore and the automatic-backup-on-startup path
>
> ### 2. Invariants — check each one mechanically, cite file:line
>
> **Money and VAT** (`CLAUDE.md` §"Money and VAT", `requeriments-barberia-v2.md` §6.3–6.4):
>
> 1. Money is `int` cents, VAT rates `int` basis points. No `decimal`/`float`/`double` in
>    storage or arithmetic. Aggregates may be `long`; if so, they must stay `long` all the
>    way to `Money.Format` — a `(int)` cast on the way to the screen is a bug.
> 2. Every sale line freezes its own price and VAT rate. A catalogue edit never moves a
>    past sale.
> 3. Breakdowns are computed **per VAT-rate group**, never per line.
> 4. Period totals are summed from the values stored on `Sale`/`SaleBreakdowns`, **never**
>    recomputed from `SaleLines`. `TillService` and `ReportsService` may read lines only to
>    count units and classify service vs. product.
> 5. Rounding is always `MidpointRounding.AwayFromZero`.
> 6. Voided sales are never deleted; every balance, VAT and ranking query filters
>    `Status == Active`.
> 7. **A recorded sale never moves in time.** Its date and time are the day the work
>    happened, not the moment a dialog was saved.
>
> **Layers** (`capa-mvvm.md` §1): ViewModels never touch EF Core (`DbContext`, `Include`,
> LINQ over `DbSet`). Services never touch the UI (`Window`, `MessageBox`, `Brush`,
> `Dispatcher`, `Application.Current`). Pure calculators stay static and dependency-free.
>
> **Presentation**: every amount reaches the screen through `Money.Format`, every rate
> through `Percentages.Format`, every enum through `Labels.Text`, every date through an
> explicit format string. No `*Cents` or `*Bp` property is ever bound in XAML. No culture
> is hardcoded — `AppLanguage.Culture` formats everything.
>
> **Async**: no `.Result`, no `.Wait()`, no `async void` outside framework overrides. A
> fire-and-forget `_ = SomeAsync()` is only acceptable where a failure cannot silently lose
> the user's work — flag every one where it can.
>
> **Language**: no user-visible literal in code; both `.resx` tables carry every key.
>
> ### 3. Hunt these specific recurrences
>
> These have each shipped at least once. Grep for them and prove the current code is clean,
> or report where it is not:
>
> - **Unit confusion at a boundary** — cents or basis points printed, bound or passed as if
>   they were euros or percent. Check XAML bindings, `StringFormat`, and every record or
>   tuple that crosses a layer.
> - **A binding that silently coerces** — binding a `TimeOnly`, `DateOnly`, `int` or enum
>   directly to a `TextBox` lets WPF's converter accept input nobody typed. Input fields
>   must bind text and parse strictly.
> - **`TimeOnly.AddMinutes` for grid or duration maths** — it wraps past midnight silently.
>   Whole minutes from midnight, via `GridHelper`, or a clamp.
> - **Unchecked `int` arithmetic on money** — a price times a quantity that overflows.
> - **A parser that guesses** rather than refusing input it cannot be certain of.
> - **A swallowed failure** — an empty `catch`, or a fire-and-forget write whose failure the
>   user never sees.
> - **A stale or missing `Include`** — a navigation property bound in a view but never
>   loaded, so a column is permanently blank.
> - **A header and its rows declared as two separate `Grid`s** with different column
>   definitions.
> - **A test that depends on the wall clock** — one that passes only on the day it was
>   written.
>
> ### 4. Then judge the quality
>
> Only after the above. Assess: responsibilities and cohesion, coupling, readability for
> somebody who did not write this, duplication that will drift, error handling and logging
> against the convention in `CLAUDE.md` §"Error handling and logging", testability, and how
> much has to change for a realistic new feature.
>
> Classify each significant abstraction as **necessary / useful / questionable / harmful**,
> with evidence from the code. An interface with one implementation that no test ever
> substitutes is indirection, not design — say so. Equally, do not call for more layers,
> more interfaces, a repository, or a split class unless you can name the concrete problem
> it solves here.
>
> Do not penalise code-behind that is genuinely the clearer home for a UI-thread concern.
> Do not turn a style preference into an architectural finding.
>
> ### 5. Rules for what you report
>
> - **Every bug claim needs a failure scenario**: concrete inputs, what the user does, what
>   they get, and why that is wrong. If you cannot write that sentence, you have a
>   suspicion, not a finding — label it as such.
> - Cite `file:line`. Quote the offending line.
> - Separate **confirmed** (you traced it or reproduced it) from **suspected**.
> - Say plainly when you lack the evidence to judge something rather than speculating.
> - Say what is genuinely good, and be specific about why.
> - Rank by impact on the user's data and money, not by how easy it is to fix.
>
> ### 6. Deliverables
>
> 1. **Live defects**, worst first: file:line, failure scenario, root cause, fix.
> 2. **Invariant violations** from section 2, each with evidence.
> 3. **Quality assessment**: what is strong, what will cause friction, abstractions
>    classified, duplication that will drift.
> 4. **Regression gaps**: which of the defects you found would a test have caught, and
>    which existing green test gave false confidence.
> 5. **A plan** split into: fix now / fix when next in that file / leave alone, with the
>    reason for each "leave alone".
> 6. One overall verdict: **excellent / healthy / acceptable / fragile / problematic**, with
>    the reasoning. No numeric score.
>
> ### 7. If you change anything
>
> Follow `CLAUDE.md`. Add a row to `bugs.csv` for every bug found, whether or not you fix
> it. Add a test for anything touching money, availability or overlap, client keys, or the
> weekly grid — and **prove the test is not vacuous** by breaking the fix, watching it fail,
> then restoring it. Keep the rebuild warning-free and the suite green before you call it
> done.

---

## Known-open items

So an audit reports these as *still open* rather than as new discoveries, and spends its
effort looking for what is not yet on this list. Remove each line as it is closed.

- `DialogService.Confirm` accepts `textConfirm`/`textCancel` and ignores both; every
  destructive confirmation shows the OS "Yes/No" instead of the concrete verb the call site
  passes. Breaks `disseny-ui` §9 in both languages.
- `SettingsViewModel` writes five settings fire-and-forget (`_ = settings.Save…`). A failed
  write is never surfaced; the toggle stays flipped and the user believes it saved.
- `MovementDialogViewModel(CashMovement)` is dead and incomplete — it restores 6 of 9
  fields, silently dropping the payment method, category and worker. It will lose data the
  day "edit a cash movement" is wired up.
- `ToWeekday` is duplicated three times (`WeekHelper`, `AvailabilityService`,
  `ReportsService`); the midnight end-time clamp is duplicated twice
  (`Appointment.EndTime`, `AvailabilityService.ClampedEnd`). One past bugfix had to patch
  two copies at once.
- Eleven service interfaces have a single implementation and are never substituted in any
  test; only `IDialogService`, `ISettingsService` and `ISoundService` are faked.
- `Texts.cs` lists every key twice (in `Keys` and as a property). `Keys` could be derived by
  reflection, removing the one omission `LanguageTests` cannot detect.
- `BackupService.Restore` copies over the live database file with no WAL/`-shm` handling and
  no post-copy integrity check. Not confirmed as a defect — needs verifying.
- `ITillService.Update`, `IAvailabilityService.IsDayOpen` and
  `IAvailabilityService.OpeningIntervals` have no callers.
