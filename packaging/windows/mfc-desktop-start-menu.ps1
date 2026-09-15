# MTDirector Desktop — Windows Start Menu shortcut sketch (DESK-HOST-WIN-01 / W7-282)
#
# Matches framework-dependent publish layout from scripts/release/package-desktop.sh:
#   OUT_DIR/desktop/Mfc.Desktop.exe
#   OUT_DIR/desktop/Mfc.Desktop.runtimeconfig.json  (net10.0)
# Default Windows RID: win-x64 (MFC_RELEASE_RID=win-x64); --self-contained false
# (host must provide .NET Desktop runtime).
#
# Install sketch (operator-owned paths; adjust $InstallRoot as needed):
#   1. Publish: MFC_RELEASE_RID=win-x64 OUT_DIR=... ./scripts/release/package-desktop.sh
#   2. Expand Mfc.Desktop-win-x64.zip to C:\mfc\desktop\
#   3. Edit C:\mfc\desktop\appsettings.json → ControllerEndpoint
#   4. Run this script elevated or as the interactive user:
#        powershell -NoProfile -ExecutionPolicy Bypass -File packaging\windows\mfc-desktop-start-menu.ps1 -InstallRoot C:\mfc\desktop
#
# Do not invent MSI/AppImage here (W7-22 lock). Linux freedesktop entry is
# DESK-HOST-LINUX-01 (packaging/linux/mfc-desktop.desktop) — do not regress that template.

[CmdletBinding()]
param(
    [Parameter()]
    [string]$InstallRoot = 'C:\mfc\desktop',

    [Parameter()]
    [string]$ShortcutName = 'MTDirector Desktop.lnk'
)

$ErrorActionPreference = 'Stop'

$exe = Join-Path $InstallRoot 'Mfc.Desktop.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Desktop entrypoint not found: $exe (expected package-desktop.sh win-x64 layout)."
}

$programs = [Environment]::GetFolderPath('Programs')
$shortcutPath = Join-Path $programs $ShortcutName

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $InstallRoot
$shortcut.Description = 'MTDirector Avalonia operator GUI (framework-dependent publish from package-desktop.sh)'
$shortcut.Save()

Write-Host "Start Menu shortcut written: $shortcutPath"
