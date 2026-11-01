param(
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$msbuild = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"
$frameworkRuntime = "C:\Windows\Microsoft.NET\Framework\v4.0.30319"
$solution = Join-Path $root "AppBar.sln"
$testExe = Join-Path $root "tests\AppBar.Tests\bin\Release\AppBar.Tests.exe"
$sourceDirectory = Join-Path $root "src\AppBar\bin\Release"
$sourceCore = Join-Path $root "src\AppBar.Core\bin\Release\AppBar.Core.dll"
$distRoot = Join-Path $root "dist"
$distDirectory = Join-Path $distRoot "AppBar"

if (-not (Test-Path -LiteralPath $msbuild)) {
    throw "MSBuild not found: $msbuild"
}

if (-not (Test-Path -LiteralPath $frameworkRuntime)) {
    throw ".NET Framework runtime not found: $frameworkRuntime"
}

Write-Host "Building AppBar..."
& $msbuild $solution /t:Rebuild /p:Configuration=Release `
    "/p:FrameworkPathOverride=$frameworkRuntime" /m:1 /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "Build failed. MSBuild exit code: $LASTEXITCODE"
}

if (-not $SkipTests) {
    Write-Host ""
    Write-Host "Running automated tests..."
    & $testExe
    if ($LASTEXITCODE -ne 0) {
        throw "Automated tests failed. Exit code: $LASTEXITCODE"
    }
}

if (-not (Test-Path -LiteralPath $sourceDirectory) -or
    -not (Test-Path -LiteralPath $sourceCore)) {
    throw "Release build output was not found."
}

$resolvedRoot = [System.IO.Path]::GetFullPath($root).TrimEnd('\')
$resolvedDist = [System.IO.Path]::GetFullPath($distDirectory).TrimEnd('\')
if (-not $resolvedDist.StartsWith($resolvedRoot + "\", [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to overwrite a directory outside the project: $resolvedDist"
}

if (Test-Path -LiteralPath $distDirectory) {
    Remove-Item -LiteralPath $distDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $distDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $sourceDirectory "AppBar.exe") -Destination $distDirectory
Copy-Item -LiteralPath $sourceCore -Destination $distDirectory

if (Test-Path -LiteralPath (Join-Path $sourceDirectory "AppBar.exe.config")) {
    Copy-Item -LiteralPath (Join-Path $sourceDirectory "AppBar.exe.config") -Destination $distDirectory
}

$launcher = Join-Path $distDirectory "AppBar.exe"
if (-not (Test-Path -LiteralPath $launcher)) {
    throw "Portable build was not generated: $launcher"
}

Write-Host ""
Write-Host "Portable build ready:"
Write-Host "  $launcher"

