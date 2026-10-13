<#
.SYNOPSIS
    Points the Debug build of EvaGest at a throwaway database, for a manual test session.

.DESCRIPTION
    The real database (%LOCALAPPDATA%\EvaGest\barberia.db) is never opened for writing:
    the session works on %LOCALAPPDATA%\EvaGest-Proves\barberia.db instead, by rewriting
    the appsettings.json next to the Debug EvaGest.exe. Run with -Restore when done.

    -Mode Fresh   : no database at all, so the app goes through its first start (new install).
    -Mode Copy    : a copy of the real database, file by file (refuses to run while
                    EvaGest is open, so the files are at rest).
    -Mode Seeded  : a fresh database filled by seed_db.py with six months of made-up history.
                    Needs Python; the app is started once by hand to create the schema first.

.EXAMPLE
    .\scripts\prepare-test-session.ps1 -Mode Fresh
    .\scripts\prepare-test-session.ps1 -Restore
#>
param(
    [ValidateSet('Fresh', 'Copy', 'Seeded')]
    [string]$Mode = 'Fresh',
    [switch]$Restore
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$exeFolder = Join-Path $repo 'EvaGest\bin\Debug\net9.0-windows'
$settingsFile = Join-Path $exeFolder 'appsettings.json'
$realDb = Join-Path $env:LOCALAPPDATA 'EvaGest\barberia.db'
$testFolder = Join-Path $env:LOCALAPPDATA 'EvaGest-Proves'
$testDb = Join-Path $testFolder 'barberia.db'

function Write-Settings([string]$dbPath) {
    $json = '{ "DatabasePath": "' + $dbPath.Replace('\', '\\') + '" }'
    # Without a byte-order mark, like the file the build ships (PowerShell 5's utf8 adds one)
    [IO.File]::WriteAllText($settingsFile, $json)
}

if (Get-Process EvaGest -ErrorAction SilentlyContinue) {
    throw 'EvaGest is open. Close it first, so no database file is in use.'
}

if ($Restore) {
    Write-Settings '%LOCALAPPDATA%\EvaGest\barberia.db'
    Write-Host 'The Debug build points at the real database again.'
    Write-Host "The test data is still in $testFolder (delete it by hand when no longer needed)."
    return
}

dotnet build (Join-Path $repo 'EvaGest\EvaGest.csproj') -c Debug --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

# A clean folder each time: a session never starts on the previous session's leftovers.
if (Test-Path $testFolder) { Remove-Item $testFolder -Recurse -Force }
New-Item -ItemType Directory -Path $testFolder | Out-Null

switch ($Mode) {
    'Fresh' {
        Write-Host 'No database: EvaGest will create one and ask for the owner PIN, as on a new install.'
    }
    'Copy' {
        if (-not (Test-Path $realDb)) { throw "There is no real database at $realDb to copy." }
        # EvaGest is closed (checked above), so the file and its WAL companions are at
        # rest and a plain copy of all three is consistent. The original is only read.
        foreach ($suffix in '', '-wal', '-shm') {
            if (Test-Path "$realDb$suffix") { Copy-Item "$realDb$suffix" "$testDb$suffix" }
        }
        Write-Host "Copied the real database to $testDb (the original was only read)."
    }
    'Seeded' {
        Write-Host 'Start EvaGest once (it creates the empty database), close it, then run:'
        Write-Host "    python `"$(Join-Path $PSScriptRoot 'seed_db.py')`" --db-path `"$testDb`""
    }
}

Write-Settings $testDb
Write-Host ''
Write-Host "Debug EvaGest now uses: $testDb"
Write-Host "Start it with: $(Join-Path $exeFolder 'EvaGest.exe')"
Write-Host 'When finished: .\scripts\prepare-test-session.ps1 -Restore'
