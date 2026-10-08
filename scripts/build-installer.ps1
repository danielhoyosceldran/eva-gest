# Builds the EvaGest installer: publishes the self-contained exe, then compiles
# installer/EvaGest.iss with Inno Setup 6. Output: installer/Output/EvaGest-Setup-<version>.exe
#
# Usage (from anywhere):  powershell -ExecutionPolicy Bypass -File scripts\build-installer.ps1
# Needs Inno Setup 6 (https://jrsoftware.org/isinfo.php) installed on this machine.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'EvaGest\EvaGest.csproj'
$publishDir = Join-Path $root 'EvaGest\bin\Publish\win-x64'
$script = Join-Path $root 'installer\EvaGest.iss'

# Find the Inno Setup compiler: on PATH, or in its two default install folders.
$iscc = (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc) {
    throw 'Inno Setup 6 not found. Install it from https://jrsoftware.org/isinfo.php'
}

# One version for the app and the installer: <Version> in EvaGest.csproj.
$version = (dotnet msbuild $project -getProperty:Version).Trim()
if ($LASTEXITCODE -ne 0 -or -not $version) { throw 'Could not read <Version> from EvaGest.csproj' }

# Start from an empty publish folder so no stale file from an older build ships.
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
dotnet publish $project -p:PublishProfile=win-x64
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

& $iscc "/DAppVersion=$version" "/DPublishDir=$publishDir" $script
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed' }

Write-Host "Installer ready: installer\Output\EvaGest-Setup-$version.exe"
