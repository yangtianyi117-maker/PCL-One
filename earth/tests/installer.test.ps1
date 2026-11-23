<#
  End-to-end installer test in a throwaway sandbox.
  Copies versions/26.3-Fabric and the real PCL.ini/servers.dat, runs install twice
  (idempotency), asserts artifacts, then uninstalls and asserts restore.

  Requires: PCL Pro installed at the default Desktop path (or pass -SourceGame).
  Does NOT touch the real .minecraft.
#>
[CmdletBinding()]
param(
    [string]$SourceGame = "",
    [string]$PclRoot = ""
)

$ErrorActionPreference = "Stop"
if (-not $SourceGame) { $SourceGame = Join-Path $env:APPDATA ".minecraft" }
if (-not $PclRoot) { $PclRoot = Join-Path $PSScriptRoot "..\..\..\vendor\pcl-pro" }

$root = Join-Path $env:TEMP ("pcl-earth-it-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
$pcl = Join-Path $root "pcl"
$game = Join-Path $root "game"
$failed = 0

function Check([string]$label, [bool]$ok) {
    if ($ok) { Write-Host "ok   $label" } else { Write-Host "FAIL $label" -ForegroundColor Red; $script:failed++ }
}

New-Item -ItemType Directory -Force -Path $pcl, $game | Out-Null
Set-Content -Path (Join-Path $pcl "PCLPro.exe") -Value "dummy" -Encoding ASCII
Copy-Item (Join-Path $SourceGame "PCL.ini") (Join-Path $game "PCL.ini") -Force
Copy-Item (Join-Path $SourceGame "servers.dat") (Join-Path $game "servers.dat") -Force
New-Item -ItemType Directory -Force -Path (Join-Path $game "versions") | Out-Null
Copy-Item (Join-Path $SourceGame "versions\26.3-Fabric") (Join-Path $game "versions\26.3-Fabric") -Recurse -Force

$distInstaller = Join-Path $PSScriptRoot "..\..\..\dist\PCL One\EarthExtension\install-earth.ps1"
$installer = if (Test-Path $distInstaller) { $distInstaller } else { Join-Path $PSScriptRoot "..\client\install-earth.ps1" }
$uninstaller = Join-Path $PSScriptRoot "..\client\uninstall-earth.ps1"

for ($i = 1; $i -le 2; $i++) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $installer -PclRoot $pcl -GameDir $game -SkipMods | Out-Null
    Check "install run $i exit 0" ($LASTEXITCODE -eq 0)
}

$jsonPath = Join-Path $game "versions\Earth-26.3-Fabric\Earth-26.3-Fabric.json"
Check "instance json exists" (Test-Path $jsonPath)
$j = Get-Content $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
Check "instance id" ($j.id -eq "Earth-26.3-Fabric")
$qp = @($j.arguments.game | Where-Object { $_ -eq "--quickPlayMultiplayer" })
Check "exactly one quickPlay" ($qp.Count -eq 1)
$idx = [Array]::IndexOf(@($j.arguments.game), "--quickPlayMultiplayer")
Check "quickPlay address" (@($j.arguments.game)[$idx + 1] -eq "124.221.231.213:25566")
Check "jar renamed" (Test-Path (Join-Path $game "versions\Earth-26.3-Fabric\Earth-26.3-Fabric.jar"))
Check "PCL.ini selects Earth" (Select-String -Path (Join-Path $game "PCL.ini") -Pattern "^Version:Earth-26.3-Fabric$" -Quiet)
Check "manifest exists" (Test-Path (Join-Path $game "pcl-earth\manifest.json"))

$dry = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "..\client\New-ServersDat.ps1") `
    -Path (Join-Path $game "servers.dat") -Name "Earth 1:1000" -Address "124.221.231.213:25566" -DryRun 2>&1
Check "servers.dat contains Earth entry" (($dry -join "`n") -match "Earth 1:1000")

& powershell -NoProfile -ExecutionPolicy Bypass -File $uninstaller -GameDir $game | Out-Null
Check "uninstall exit 0" ($LASTEXITCODE -eq 0)
Check "instance removed" (-not (Test-Path (Join-Path $game "versions\Earth-26.3-Fabric")))
Check "PCL.ini restored" (Select-String -Path (Join-Path $game "PCL.ini") -Pattern "^Version:26.3-Fabric$" -Quiet)
Check "source json restored" ((Get-Content (Join-Path $game "versions\26.3-Fabric\26.3-Fabric.json") -Raw | ConvertFrom-Json).id -eq "26.3-Fabric")

Write-Host ("sandbox: {0}" -f $root)
if ($failed -gt 0) {
    Write-Host ("{0} check(s) failed" -f $failed) -ForegroundColor Red
    exit 1
}
Write-Host "all installer checks passed"
exit 0
