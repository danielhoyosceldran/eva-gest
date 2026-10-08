# Changelog

Installer / app releases. The version is `<Version>` in `EvaGest/EvaGest.csproj`;
the installer file is `installer/Output/EvaGest-Setup-<version>.exe`.

## 1.1.0 — in progress

- Deleting an appointment no longer asks for the owner's PIN, only "are you sure?".
  Deleting clients, sales, till movements and catalogue items still asks for it.
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

## 1.0.0

First installer. Self-contained win-x64 exe, Inno Setup, per-user install,
replaces itself on upgrade (fixed `AppId`).
