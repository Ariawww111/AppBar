param(
    [string]$SnapshotPath,
    [string]$DataDirectory,
    [string]$ResultPath
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($SnapshotPath)) {
    $SnapshotPath = Join-Path $root "state-before-fix.json"
}

if ([string]::IsNullOrWhiteSpace($DataDirectory)) {
    $DataDirectory = Join-Path $env:LOCALAPPDATA "AppBar"
}

if ([string]::IsNullOrWhiteSpace($ResultPath)) {
    $ResultPath = Join-Path $root "state-restore-result.txt"
}

$SnapshotPath = [System.IO.Path]::GetFullPath($SnapshotPath)
$DataDirectory = [System.IO.Path]::GetFullPath($DataDirectory)
$ResultPath = [System.IO.Path]::GetFullPath($ResultPath)
$StatePath = Join-Path $DataDirectory "state.json"

if (-not (Test-Path -LiteralPath $SnapshotPath)) {
    throw "Snapshot not found: $SnapshotPath"
}

$snapshotJson = [System.IO.File]::ReadAllText($SnapshotPath)
$snapshotState = $snapshotJson | ConvertFrom-Json
$snapshotCount = @($snapshotState.Apps).Count
if ($snapshotCount -eq 0) {
    throw "Snapshot does not contain any applications."
}

New-Item -ItemType Directory -Path $DataDirectory -Force | Out-Null
$temporaryPath = $StatePath + ".restore.tmp"
[System.IO.File]::WriteAllText(
    $temporaryPath,
    $snapshotJson,
    (New-Object System.Text.UTF8Encoding($false)))

Move-Item -LiteralPath $temporaryPath -Destination $StatePath -Force
Copy-Item -LiteralPath $StatePath -Destination ($StatePath + ".bak") -Force
Copy-Item -LiteralPath $StatePath -Destination ($StatePath + ".last-good") -Force

$restoredJson = [System.IO.File]::ReadAllText($StatePath)
$restoredState = $restoredJson | ConvertFrom-Json
$restoredCount = @($restoredState.Apps).Count
if ($restoredCount -ne $snapshotCount) {
    throw "Restored app count mismatch: expected $snapshotCount, actual $restoredCount"
}

$result = @(
    "Restored at: " + (Get-Date).ToString("yyyy-MM-dd HH:mm:ss.fff")
    "User: " + [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    "Snapshot: " + $SnapshotPath
    "State: " + $StatePath
    "Apps: " + $restoredCount
)
$result | Set-Content -LiteralPath $ResultPath -Encoding UTF8
$result
