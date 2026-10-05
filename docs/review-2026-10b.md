# Code review — October 2026, second pass

A second read-only review on 2026-10-05, after everything in
[`review-2026-10.md`](review-2026-10.md) was settled. It found what that pass had
not raised; nothing already accepted or fixed there was raised again. This page
records each finding, what the owner decided, and the commit that carries it out.
The same bugs have rows in [`bugs.csv`](../bugs.csv).

Overall verdict: **usable with one fix**. The one that mattered was that the daily
automatic backup almost never ran.

## Fixed

| ID | Severity | Problem | Owner's decision and fix | Commit |
|---|---|---|---|---|
| F-01 | Critical | The automatic backup was only checked at startup, and only once the configured hour (20:00) had passed. A shop that opens the app in the morning and closes before 20:00 never got one, a missed day was never caught up, and nothing said so. | **On closing, take the day's automatic copy if it has not been taken yet, whatever the hour; at startup, catch up a day that ended without its copy.** Closing waits for the copy; a failure is told and the app closes anyway. Closing copies are `*_close.db`, listed as automatic. A closing copy covers its day, so the next morning does not repeat it. Not taken when the app closes itself after a restore. | `2114a1f` |
| F-02 | Medium | Esc, Cancel and the X closed the sale and appointment dialogs at once, losing a half-entered sale. | **Ask before discarding.** Both dialogs compare what they hold with what they opened with; when something changed, closing asks *Descartar / Continuar editant*. An untouched dialog closes at once. The other dialogs are unchanged. | `5b8d975` |
| F-03 | Medium | Re-exporting a period while its CSV was open in Excel failed with the generic error, and the files were written one by one, so the folder could hold a mix of the new and the old export. | **Implemented as proposed.** Every file is built first, none is written if one is open (the dialog names it and says to close it), and they are written as `*.tmp` and moved into place together. A folder that cannot be written says to pick another. | `3b3a98b` |
| F-04 | Medium | Deleting a payment method cascaded, in the database, to every sale and cash movement paid with it; only a check in the app prevented it. | **Deactivate and reactivate stay** (the button now reads *Desactivar / Reactivar*, like workers). **Deleting a used method removes it for good after a warning** that its sales and movements will be left with no payment method. They are kept, amounts unchanged, shown as *Mètode eliminat*, each change in the audit trail. The database now sets the method to null instead of cascading (migration `KeepRecordsWhenPaymentMethodDeleted`). The last active method still cannot be deleted. | `45fafe9` |
| F-05 | Low | Any worker could delete a client (and their appointments) with owner mode closed; voiding from the sale dialog and removing a closed day asked nothing. | **Every delete and void asks for the owner's PIN, even with owner mode open**: client, appointment, service, product, payment method, expense category, worker, closed day, cash movement, voiding or deleting a sale, and cutting the number of backups kept. Checking the PIN shares the lockout with unlocking and does not open or close owner mode. Before a PIN exists, the plain question is asked. Status changes of an appointment (cancelled, no-show) are not deletes and do not ask. | `3576b41` |
| F-06 | Low | SQLite's backup API never waits on a lock, so a backup or restore that coincided with a write failed at once with "database is locked". | **Solved.** The copy is retried for about two seconds, each retry logged. A lock held longer still fails. | `9d85958` |
| F-07 | Low | A failure after the database opened (language, automatic backup, main window) left an invisible process holding the single-instance lock, so every later launch said EvaGest was already open. | **Solved.** The failure is logged as Fatal, the user is told, and the app exits. | `65d4985` |
| F-09 | Low | Agenda grid clicks and the reloads on a filter change (Agenda, Clients, Sales, Till, Reports) were fire-and-forget: a failure was never seen. | **Solved.** They go through `PageViewModelBase.RunInBackground`, which logs the failure and shows a notice on the page. Clients, Reports and Till now show the page notice line. | `018b65a` |

### Behaviour that now differs from the spec's wording

- **CU-11 (automatic backup).** The spec says the copy is taken at the configured hour
  if the app is open, otherwise at the next startup. Now it is taken on closing the
  app if the day has none yet, and the next startup catches up a day that ended
  without one. The configured hour still counts: a startup after it, on a day with no
  copy, takes one.
- **RF-21 / pantalles 2.5 (deleting).** Deleting asks for the owner's PIN, not just a
  yes/no. A payment method used by sales is now deleted for good when the owner
  confirms the warning, instead of being deactivated.

## Documented, not changed

| ID | Finding | Decision |
|---|---|---|
| F-08 | Booking a named worker on their day off, or outside their own hours, gives no warning: `AvailabilityService.Check` only counts that worker's overlapping appointments and checks the shop's opening hours. This matches CU-01b as written (only the unassigned case looks at workers' schedules). | **Left as it is, by the owner's decision.** If it is wanted later: reuse `AvailableWorkers` for the chosen worker and add a "not working then" notice next to the overlap one. |

## Tests

79 regression tests (`3f4d41e`), written by a separate tester pass from each problem's
description and the owner's decision, not from the fix. Each problem test was shown to
fail without its fix: on the commit before it where the test compiles there (F-01
startup, F-02, F-05 pages, F-06, F-09), and otherwise by breaking the fix's behaviour
deliberately (F-01 closing, F-03, F-04, the F-05 PIN dialog). Six existing tests that
asserted the old behaviour were updated in the fix commits themselves (and one more was
strengthened), each named in its commit message.

**No automated test** for what needs the real window or the real App. Manual checks:

1. **F-01.** Close EvaGest on a day with no automatic copy: the window waits, then a
   `*_close.db` appears in `Backups`. Close it again the same day: no second copy. Make
   the copy fail (for example a full disk): the message appears and the app still closes.
   After a restore, no closing copy is taken.
2. **F-02.** In a new sale, add a line and press Esc, then the X: both ask; *Continuar
   editant* keeps the window. Open and close an untouched sale or appointment: no
   question.
3. **F-04.** Catàleg › Mètodes de pagament: the button reads *Desactivar* on an active
   method and *Reactivar* on an inactive one. Deleting a used method shows how many
   records it is on, in Catalan and Spanish.
4. **F-05.** Delete anything with owner mode open: the PIN is asked; Enter confirms; a
   wrong PIN keeps the dialog open; five wrong ones show the lockout countdown.
5. **F-07.** Force a failure after the database opens: the message appears, no
   `EvaGest.exe` is left in Task Manager, and the next launch opens normally.

## Left open

- A closing copy covers its whole day: if the app is closed at 13:00 and reopened in the
  afternoon, the afternoon's work waits for the next day's copy. Shutting Windows down
  with the app open takes no closing copy either; the next morning catches it up.
- The F-06 retry gives up after about two seconds; a longer lock still fails with the
  generic error.
- The F-03 check for an open file and the final move are separate steps; a file opened
  between them can still fail the move.
- `dotnet test` now takes about 15 seconds of test execution: the lock and restore tests
  wait on real locks.
- `WorkerServiceTests.The_page_lists_the_workers_with_a_summarised_schedule` failed once
  during this work and passed on every rerun, most likely the culture race already in
`bugs.csv`, still open.
