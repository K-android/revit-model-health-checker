<#
.SYNOPSIS
  Builds BIMHealthCheck for every supported Revit version and packages it in dist\.

.EXAMPLE
  .\build.ps1                          # build 2024, 2025, 2026
  .\build.ps1 -Versions 2026 -Deploy   # build 2026 and install it for the current user
#>
param(
    [string[]]$Versions = @("2024", "2025", "2026"),
    [string]$Configuration = "Release",
    [switch]$Deploy
)

$ErrorActionPreference = "Stop"
$root    = $PSScriptRoot
$project = Join-Path $root "BIMHealthCheck\BIMHealthCheck.csproj"
$dist    = Join-Path $root "dist"
$failed  = @()

foreach ($v in $Versions) {
    Write-Host "`n=== Building for Revit $v ===" -ForegroundColor Cyan
    $extra = @()
    if ($Deploy) { $extra += "-p:Deploy=true" }
    dotnet build $project -c $Configuration -p:RevitVersion=$v -nologo @extra
    if ($LASTEXITCODE -ne 0) { $failed += $v; continue }

    # Package: dist\<ver>\BIMHealthCheck.addin + dist\<ver>\BIMHealthCheck\BIMHealthCheck.dll
    $out    = Join-Path $root "bin\$Configuration\$v"
    $target = Join-Path $dist $v
    New-Item -ItemType Directory -Force -Path (Join-Path $target "BIMHealthCheck") | Out-Null
    Copy-Item (Join-Path $out "BIMHealthCheck.addin") $target -Force
    Copy-Item (Join-Path $out "BIMHealthCheck.dll")   (Join-Path $target "BIMHealthCheck") -Force
}

if ($failed.Count -gt 0) {
    Write-Host "`nBuild FAILED for: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "`nAll builds succeeded. Packages are in $dist" -ForegroundColor Green
