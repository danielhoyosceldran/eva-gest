; EvaGest installer (Fase 11, delivery). Compiled by scripts/build-installer.ps1,
; which publishes the app first and passes AppVersion and PublishDir in.
;
; Kept deliberately small: the requirements ask for "no elaborate wizard"
; (requeriments-barberia-v2.md), so the user sees a welcome page, the shortcut
; choices and the install button — no folder or component pages.
;
; What the installer does NOT do, because the app already does it on first run:
; create, migrate or seed the database. Data lives in %LOCALAPPDATA%\EvaGest,
; outside {app}, so installing, updating or uninstalling never touches it.

#ifndef AppVersion
  #error AppVersion is not defined. Build through scripts/build-installer.ps1.
#endif
#ifndef PublishDir
  #error PublishDir is not defined. Build through scripts/build-installer.ps1.
#endif

[Setup]
; Never change this id: it is how a new version recognises and replaces the old one.
AppId={{1D5C1DA5-1CF1-4C66-821B-C6B4EEF635C2}
AppName=EvaGest
AppVersion={#AppVersion}
AppVerName=EvaGest {#AppVersion}
AppPublisher=EvaGest

; Per-user install: no administrator prompt, and {userstartup}/{userdesktop}
; below resolve to the account that runs the installer.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\EvaGest
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
UninstallDisplayIcon={app}\EvaGest.exe

; The publish is a self-contained win-x64 build.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Close a running EvaGest before replacing its exe, and don't restart it from
; here: the finish page offers to open it instead.
CloseApplications=force
RestartApplications=no

OutputDir=Output
OutputBaseFilename=EvaGest-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

; Show the language picker only if the Windows language is neither of these two.
ShowLanguageDialog=auto

[Languages]
Name: "catalan"; MessagesFile: "compiler:Languages\Catalan.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[CustomMessages]
catalan.StartWithWindows=Obrir EvaGest en engegar l'ordinador
spanish.StartWithWindows=Abrir EvaGest al encender el ordenador

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"
; Off by default; the choice is remembered on later updates (UsePreviousTasks).
Name: "startup"; Description: "{cm:StartWithWindows}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\EvaGest.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\EvaGest"; Filename: "{app}\EvaGest.exe"
Name: "{userdesktop}\EvaGest"; Filename: "{app}\EvaGest.exe"; Tasks: desktopicon
; The current user's Startup folder (shell:startup). Never a hardcoded path:
; the account on the shop's PC is not the developer's.
Name: "{userstartup}\EvaGest"; Filename: "{app}\EvaGest.exe"; Tasks: startup

[Run]
Filename: "{app}\EvaGest.exe"; Description: "{cm:LaunchProgram,EvaGest}"; Flags: nowait postinstall skipifsilent

; No [UninstallDelete]: %LOCALAPPDATA%\EvaGest (database, backups, logs) is the
; shop's data and must survive an uninstall.
