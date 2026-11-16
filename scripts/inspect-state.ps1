param(
    [string]$DataDirectory,
    [string]$ResultPath
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($DataDirectory)) {
    $DataDirectory = Join-Path $env:LOCALAPPDATA "AppBar"
}

if ([string]::IsNullOrWhiteSpace($ResultPath)) {
    $ResultPath = Join-Path $root "state-inspect-result.txt"
}

$DataDirectory = [System.IO.Path]::GetFullPath($DataDirectory)
$ResultPath = [System.IO.Path]::GetFullPath($ResultPath)
$StatePath = Join-Path $DataDirectory "state.json"
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("Inspected at: " + (Get-Date).ToString("yyyy-MM-dd HH:mm:ss.fff"))
$lines.Add("User: " + [System.Security.Principal.WindowsIdentity]::GetCurrent().Name)
$lines.Add("LocalAppData: " + $env:LOCALAPPDATA)
$lines.Add("DataDirectory: " + $DataDirectory)

foreach ($name in @("state.json", "state.json.bak", "state.json.last-good")) {
    $path = Join-Path $DataDirectory $name
    if (-not (Test-Path -LiteralPath $path)) {
        $lines.Add($name + ": missing")
        continue
    }

    $file = Get-Item -LiteralPath $path
    $text = [System.IO.File]::ReadAllText($path)
    try {
        $state = $text | ConvertFrom-Json
        $count = @($state.Apps).Count
        $summary = "${name}: bytes=$($file.Length); apps=$count; "
        $summary += "schema=$($state.SchemaVersion); "
        $summary += "emptyConfirmed=$($state.EmptyStateConfirmed)"
        $lines.Add($summary)
    }
    catch {
        $lines.Add($name + ": unreadable; " + $_.Exception.Message)
    }
}

$logPath = Join-Path $DataDirectory "startup.log"
if (Test-Path -LiteralPath $logPath) {
    $lines.Add("")
    $lines.Add("Recent startup log:")
    try {
        $lines.AddRange([string[]](Get-Content -Tail 30 -LiteralPath $logPath))
    }
    catch {
        $lines.Add("unreadable: " + $_.Exception.Message)
    }
}

$lines | Set-Content -LiteralPath $ResultPath -Encoding UTF8
$lines
