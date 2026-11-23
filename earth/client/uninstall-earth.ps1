<#
.SYNOPSIS
  Undo install-earth.ps1 using its manifest.

.DESCRIPTION
  - refuses to run while PCLPro/javaw is running
  - moves the Earth instance aside (does not hard-delete)
  - restores PCL.ini / servers.dat / source version JSON from the install backup
  - removes the shortcut created by the installer
#>
[CmdletBinding()]
param(
    [string]$GameDir = ""
)

$ErrorActionPreference = "Stop"

if (-not $GameDir) { $GameDir = Join-Path $env:APPDATA ".minecraft" }
$earthDir = Join-Path $GameDir "pcl-earth"
$manifestPath = Join-Path $earthDir "manifest.json"

if (-not (Test-Path $manifestPath)) {
    Write-Host "[uninstall][ERROR] manifest not found: $manifestPath" -ForegroundColor Red
    exit 2
}

$running = @(Get-Process -Name PCLPro, javaw, java -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Write-Host "[uninstall][ERROR] close PCL Pro / Minecraft first" -ForegroundColor Red
    exit 3
}

$m = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
Write-Host "[uninstall] instance: $($m.instance)"

$instanceDir = Join-Path $GameDir "versions\$($m.instance)"
if (Test-Path $instanceDir) {
    $aside = Join-Path $earthDir "uninstalled-$stamp"
    New-Item -ItemType Directory -Force -Path $aside | Out-Null
    Move-Item $instanceDir (Join-Path $aside $m.instance) -Force
    Write-Host "[uninstall] instance moved to $aside"
}

$currentBackup = Join-Path $earthDir "before-uninstall-$stamp"
New-Item -ItemType Directory -Force -Path $currentBackup | Out-Null

foreach ($name in @("PCL.ini", "servers.dat", "26.3-Fabric.json")) {
    $src = Join-Path $m.backupDir $name
    if (-not (Test-Path $src)) { continue }
    if ($name -eq "26.3-Fabric.json") {
        $dst = Join-Path $GameDir "versions\26.3-Fabric\26.3-Fabric.json"
    } else {
        $dst = Join-Path $GameDir $name
    }
    if (Test-Path $dst) { Copy-Item $dst (Join-Path $currentBackup $name) -Force }
    Copy-Item $src $dst -Force
    Write-Host "[uninstall] restored $dst"
}

if ($m.shortcut -and (Test-Path $m.shortcut)) {
    Remove-Item $m.shortcut -Force
    Write-Host "[uninstall] removed $($m.shortcut)"
}

Move-Item $manifestPath (Join-Path $earthDir "manifest.uninstalled-$stamp.json") -Force
Write-Host "[uninstall] DONE"
exit 0
