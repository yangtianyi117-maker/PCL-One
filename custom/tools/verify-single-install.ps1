# PCL Pro single-install invariant check
# Exit code 0 = all PASS, 1 = at least one FAIL.
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify-single-install.ps1

$ErrorActionPreference = 'Continue'
$script:fail = 0

function Check([string]$name, [bool]$ok, [string]$detail) {
    if ($ok) { Write-Host ("PASS  {0}  {1}" -f $name, $detail) }
    else { Write-Host ("FAIL  {0}  {1}" -f $name, $detail); $script:fail++ }
}

$desktop = [Environment]::GetFolderPath('Desktop')
$install = Join-Path $desktop 'PCL Pro'
$exe     = Join-Path $install 'PCLPro.exe'
$runtime = Join-Path $install 'PCLProRuntime'
$data    = Join-Path $runtime 'PCL'
$mc      = Join-Path $env:APPDATA '.minecraft'
$expectedPayload = 'pclpro-2026-11-21.17'

# 1. exactly one launcher exe in the install dir, no stale copies
$exes = @(Get-ChildItem -LiteralPath $install -File -Filter '*.exe' -ErrorAction SilentlyContinue)
$stale = @(Get-ChildItem -LiteralPath $install -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '(?i)\.(bak|old|exe\.)' })
Check 'install-exe' ($exes.Count -eq 1 -and $exes[0].Name -eq 'PCLPro.exe') ("found {0} exe(s): {1}" -f $exes.Count, (($exes | ForEach-Object Name) -join ','))
Check 'install-no-stale' ($stale.Count -eq 0) ("stale: {0}" -f (($stale | ForEach-Object Name) -join ','))

# 2. no second install on the Desktop
$backups = @(Get-ChildItem -LiteralPath $desktop -Directory -Force -ErrorAction SilentlyContinue | Where-Object { $_.Name -like 'PCL Pro.bak*' -or $_.Name -like 'PCLPro*' })
Check 'desktop-no-backup' ($backups.Count -eq 0) ("dirs: {0}" -f (($backups | ForEach-Object Name) -join ','))

# 3. McLauncher fully gone
$ml = @(
    (Join-Path $desktop 'McLauncher.exe'),
    (Join-Path $desktop '.state'),
    (Join-Path $env:APPDATA 'McLauncher'),
    (Join-Path $env:LOCALAPPDATA 'McLauncher')
) | Where-Object { Test-Path -LiteralPath $_ }
Check 'mclauncher-gone' (@($ml).Count -eq 0) ("leftovers: {0}" -f (@($ml) -join ','))

# 4. payload version
$payloadFile = Join-Path $runtime '.payload-version'
$payload = if (Test-Path -LiteralPath $payloadFile) { (Get-Content -LiteralPath $payloadFile -Raw).Trim() } else { '' }
Check 'payload-version' ($payload -ne '') ("payload = {0} (expected >= {1})" -f $payload, $expectedPayload)

# 5. single data root
Check 'data-root' (Test-Path -LiteralPath $data) $data
Check 'data-config' (Test-Path -LiteralPath (Join-Path $data 'config.v1.yml')) 'config.v1.yml'
Check 'data-log' ((@(Get-ChildItem -LiteralPath (Join-Path $data 'Log') -File -ErrorAction SilentlyContinue)).Count -gt 0) 'Log\ has entries'

# 6. phone bridge is gone: client no longer ships Geyser / QR
$geyser = Join-Path $data 'Geyser\Geyser-Standalone.jar'
Check 'no-phone-bridge' (-not (Test-Path -LiteralPath $geyser)) 'PCL\Geyser\Geyser-Standalone.jar must not exist'

# 7. fused user assets
$world = Join-Path $mc 'saves\New World\level.dat'
Check 'fused-world' (Test-Path -LiteralPath $world) $world
$mods = @(Get-ChildItem -LiteralPath (Join-Path $mc 'mods') -File -Filter *.jar -ErrorAction SilentlyContinue)
Check 'fused-mods' ($mods.Count -ge 1) ("{0} jar(s)" -f $mods.Count)

# 8. install == release artifact
$dist = Join-Path (Split-Path -Parent $PSScriptRoot) 'dist\PCLPro.exe'
if (Test-Path -LiteralPath $dist) {
    $h1 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
    $h2 = (Get-FileHash -LiteralPath $dist -Algorithm SHA256).Hash
    Check 'install-equals-dist' ($h1 -eq $h2) ("{0} vs {1}" -f $h1.Substring(0,16), $h2.Substring(0,16))
} else {
    Write-Host ("SKIP  install-equals-dist  dist\PCLPro.exe not present")
}

Write-Host ''
if ($script:fail -eq 0) { Write-Host 'RESULT: OK (single install invariant holds)'; exit 0 }
Write-Host ("RESULT: {0} check(s) failed" -f $script:fail); exit 1
