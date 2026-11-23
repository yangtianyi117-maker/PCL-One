<#
  Preflight for the PCL Earth server host.
  Checks local build inputs and, when SSH is reachable, the remote disk/RAM/Java/ports.
#>
[CmdletBinding()]
param(
    [string]$SshHost = "124.221.231.213",
    [string]$SshUser = "root",
    [int]$SshPort = 22,
    [string]$KeyFile = "",
    [string]$LocalWorld = ""
)

$ErrorActionPreference = "Continue"
$failed = 0

function Check([string]$label, [bool]$ok, [string]$detail = "") {
    if ($ok) { Write-Host ("ok   {0} {1}" -f $label, $detail) }
    else { Write-Host ("FAIL {0} {1}" -f $label, $detail) -ForegroundColor Red; $script:failed++ }
}

Write-Host "== local =="
$free = (Get-PSDrive C).Free / 1GB
Check "C: free >= 60 GB (staging)" ($free -ge 60) ("({0:N1} GB)" -f $free)
Check "client installer" (Test-Path (Join-Path $PSScriptRoot "..\..\client\install-earth.ps1"))
if ($LocalWorld) {
    Check "world level.dat" (Test-Path (Join-Path $LocalWorld "level.dat")) $LocalWorld
    Check "world region" (Test-Path (Join-Path $LocalWorld "region"))
}

Write-Host "== remote ($SshUser@$SshHost) =="
$sshArgs = @("-o", "BatchMode=yes", "-o", "ConnectTimeout=6", "-p", "$SshPort")
if ($KeyFile) { $sshArgs += @("-i", $KeyFile) }
$target = "$SshUser@$SshHost"
$remote = @'
echo "--- uname ---"; uname -a
echo "--- disk ---"; df -h /
echo "--- mem ---"; free -m | head -2
echo "--- java ---"; java -version 2>&1 | head -1
echo "--- docker ---"; docker --version 2>&1 | head -1
echo "--- ports ---"; (ss -lunpt 2>/dev/null || netstat -lunpt 2>/dev/null) | grep -E '25565|19132|8100|25575' || true
'@
$out = & ssh @sshArgs $target $remote 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ssh failed; run these manually on the host:" -ForegroundColor Yellow
    Write-Host $remote
    $failed++
} else {
    $out | ForEach-Object { Write-Host $_ }
    $disk = ($out | Select-String -Pattern '/\s+.*\s(\d+)G\s+').Matches
    Check "remote reachable" $true
}

if ($failed -gt 0) { Write-Host ("{0} preflight issue(s)" -f $failed) -ForegroundColor Red; exit 1 }
Write-Host "preflight passed"
exit 0
