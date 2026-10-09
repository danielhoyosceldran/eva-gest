# Changelog

Installer / app releases. The version is `<Version>` in `EvaGest/EvaGest.csproj`;
the installer file is `installer/Output/EvaGest-Setup-<version>.exe`.

## 1.1.1

- Restoring a backup made by an older version (1.0) now finishes and closes the app as
  it should. It used to report an error after the data had already been restored and
  leave the app open on it.
- If EvaGest cannot start because its settings file or data folder is missing or
  unusable, it now says so and closes. It used to stay running invisibly, and every
  later attempt to open it said it was already open.
- If the appointment window cannot load its data, it now says so and does not let the
  appointment be saved. It used to open half empty without a word, and saving an
  edited appointment then removed its service and worker.
- A backup that fails while it is being written (disk full, data file busy) no longer
  leaves an empty file behind. That file used to count as the day's copy, so the
  automatic backup was not tried again, and it showed up in the list to restore.
- A backup that was taken correctly is no longer reported as failed when tidying up
  the old copies afterwards runs into a problem.
- A payment method or expense category can no longer be given a name that already
  exists (a deactivated one included). The window now says so and keeps what was typed,
  instead of closing on an unexpected error.
- The sale window now closes as soon as the sale is saved. If something failed just
  after saving, it could stay open and a second Cobrar charged the same sale twice.
- When "Fer còpia ara" or a restore cannot be done (disk full, data file busy), the
  page or the restore window now says so and what to try, instead of a generic error.
- The windows for services, products, payment methods, categories, clients, till
  movements and workers now save before they close. If saving fails they stay open,
  say so, and keep what was typed; before, it was lost behind a generic error.
- An error that keeps repeating now shows its message once (at most once a minute)
  instead of one window after another, and an error with no main window open closes
  the app instead of leaving it running invisibly.

## 1.1.0

- Deleting an appointment no longer asks for the owner's PIN, only "are you sure?".
  Deleting clients, sales, till movements and catalogue items still asks for it.
- The action buttons at the end of each table (Editar, Anul·lar, Eliminar…) always show in full:
  that column takes the width its buttons need and the other columns share the rest.
  The Inici appointments table now drops its least important columns on a narrow screen.
- The message shown when a dialog can't be saved (missing or invalid fields) is now a
  bold red block instead of thin red text, so it is hard to miss.
- Works on laptop screens (down to 1366x768 at 125 % scaling):
  - Dialogs never grow past the screen. When the form is taller than the screen it
    scrolls, and the buttons (Guardar, Cobrar, Cancel·lar) and the error message stay
    visible underneath it. A sale with many lines no longer hides Cobrar.
  - The appointment dialog's week picker narrows to fit the screen.
  - The navigation scrolls when it doesn't fit, so Bloquejar is always reachable.
  - Summary cards (Inici, Caixa, Informes) wrap onto a second row instead of cutting
    off amounts.
  - The sales list hides base, VAT, worker and payment columns on a narrow screen
    (the totals stay in the footer) instead of hiding the client and concept.
  - The Agenda's date range moves under the buttons when there is no room beside
    them, and the day panel narrows so the week stays readable.
  - The sales chart in Informes keeps every month in view; long names wrap or show
    in full in a tooltip.
- Closing the app a second time on the same day no longer fails with an error.
- In the sale dialog, "+ Servei" and "+ Producte" stay greyed out until something is
  picked in the box next to them, instead of failing with an error. After adding, the
  box empties again.
- With the language set to Spanish, every page now shows in Spanish. Only the
  navigation did before; the pages themselves stayed in Catalan (or in the Windows
  language).

## 1.0.0

First installer. Self-contained win-x64 exe, Inno Setup, per-user install,
replaces itself on upgrade (fixed `AppId`).
