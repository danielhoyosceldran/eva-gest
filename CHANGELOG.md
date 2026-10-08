# Changelog

Installer / app releases. The version is `<Version>` in `EvaGest/EvaGest.csproj`;
the installer file is `installer/Output/EvaGest-Setup-<version>.exe`.

## 1.1.0 — in progress

- Deleting an appointment no longer asks for the owner's PIN, only "are you sure?".
  Deleting clients, sales, till movements and catalogue items still asks for it.

## 1.0.0

First installer. Self-contained win-x64 exe, Inno Setup, per-user install,
replaces itself on upgrade (fixed `AppId`).
