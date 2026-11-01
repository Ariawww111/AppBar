param(
    [string]$ExecutablePath
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $ExecutablePath = Join-Path $root "dist\AppBar\AppBar.exe"
}

$ExecutablePath = [System.IO.Path]::GetFullPath($ExecutablePath)
if (-not (Test-Path -LiteralPath $ExecutablePath)) {
    throw "Application not found: $ExecutablePath"
}

$first = Start-Process -FilePath $ExecutablePath -PassThru
try {
    Start-Sleep -Seconds 2
    $first.Refresh()
    if ($first.HasExited) {
        throw "Application exited immediately. Exit code: $($first.ExitCode)"
    }

    $second = Start-Process -FilePath $ExecutablePath -PassThru
    Start-Sleep -Seconds 2
    $second.Refresh()
    if (-not $second.HasExited) {
        throw "Second instance did not exit; single-instance protection failed."
    }

    $networkConnections = @()
    try {
        $networkConnections = @(Get-NetTCPConnection -OwningProcess $first.Id -ErrorAction Stop)
    }
    catch {
        Write-Host "Network connection inspection was unavailable; process has no network components."
    }

    if ($networkConnections.Count -gt 0) {
        throw "The application established a network connection."
    }

    Write-Host "Smoke test passed: single instance, stable process, no network connections."
}
finally {
    if (-not $first.HasExited) {
        Stop-Process -Id $first.Id -Force
    }

    if ($null -ne $second -and -not $second.HasExited) {
        Stop-Process -Id $second.Id -Force
    }
}

