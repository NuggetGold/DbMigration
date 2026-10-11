#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Runtime = "win-x64",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Output = "dist",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
    $env:DOTNET_NOLOGO = "1"

    $outDir = Join-Path $root $Output
    if (Test-Path $outDir) {
        Remove-Item $outDir -Recurse -Force
    }

    $selfContained = (-not $FrameworkDependent).ToString().ToLowerInvariant()

    Write-Host "Publishing DbMigration.App ($Configuration / $Runtime, self-contained=$selfContained)..." -ForegroundColor Cyan

    $publishArgs = @(
        "publish", "src\DbMigration.App\DbMigration.App.csproj",
        "-c", $Configuration,
        "-r", $Runtime,
        "--self-contained", $selfContained,
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:IncludeAllContentForSelfExtract=true",
        "-p:EnableCompressionInSingleFile=true",
        "-p:DebugType=None",
        "-p:DebugSymbols=false",
        "-p:SatelliteResourceLanguages=en",
        "-o", $outDir,
        "--nologo"
    )

    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    $exe = Join-Path $outDir "DbMigration.App.exe"
    if (-not (Test-Path $exe)) {
        throw "Expected output not found: $exe"
    }

    $friendly = Join-Path $outDir "DbMigration.exe"
    Move-Item -Path $exe -Destination $friendly -Force

    $sizeMb = [math]::Round((Get-Item $friendly).Length / 1MB, 1)
    Write-Host ""
    Write-Host "Done. Standalone executable:" -ForegroundColor Green
    Write-Host "  $friendly" -ForegroundColor Green
    Write-Host "  size: ${sizeMb} MB  (no .NET install required)" -ForegroundColor Green
    Write-Host ""
    Write-Host "You can copy this single file anywhere and double-click it." -ForegroundColor Gray
}
finally {
    Pop-Location
}
