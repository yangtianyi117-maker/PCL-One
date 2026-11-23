<#
  Remote restore from a backup archive. Keeps the broken world aside.
  Example:
    .\restore.ps1 -Backup world-2026-11-23-0400.tar.gz
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Backup,
    [string]$SshHost = "124.221.231.213",
    [string]$SshUser = "root",
    [int]$SshPort = 22,
    [string]$KeyFile = "",
    [string]$RemoteDir = "/srv/pcl-earth",
    [string]$WorldName = "Earth1000",
    [switch]$Start
)

$ErrorActionPreference = "Stop"
$sshArgs = @("-o", "BatchMode=yes", "-p", "$SshPort")
if ($KeyFile) { $sshArgs += @("-i", $KeyFile) }
$target = "$SshUser@$SshHost"

$script = @"
set -euo pipefail
cd '$RemoteDir'
test -f "backups/$Backup"
docker compose stop mc
ts=`$(date +%F-%H%M%S)
mv "data/$WorldName" "data/$WorldName.broken-`$ts"
tar xzf "backups/$Backup" -C data
docker compose start mc
docker compose logs --tail=40 mc
"@

& ssh @sshArgs $target $script
if ($LASTEXITCODE -ne 0) { throw "restore failed" }
Write-Host "restore done"
