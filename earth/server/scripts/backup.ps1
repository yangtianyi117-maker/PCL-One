<#
  Remote backup: save-off/save-all, tar the world, save-on, prune.
  The archive stays on the host (private directory). Never expose it over HTTP.
#>
[CmdletBinding()]
param(
    [string]$SshHost = "124.221.231.213",
    [string]$SshUser = "root",
    [int]$SshPort = 22,
    [string]$KeyFile = "",
    [string]$RemoteDir = "/srv/pcl-earth",
    [string]$WorldName = "Earth1000",
    [int]$KeepDays = 7
)

$ErrorActionPreference = "Stop"
$sshArgs = @("-o", "BatchMode=yes", "-p", "$SshPort")
if ($KeyFile) { $sshArgs += @("-i", $KeyFile) }
$target = "$SshUser@$SshHost"

$script = @"
set -euo pipefail
cd '$RemoteDir'
ts=`$(date +%F-%H%M)
mkdir -p backups
docker compose exec -T mc rcon-cli save-off || true
docker compose exec -T mc rcon-cli save-all || true
tar czf "backups/world-`$ts.tar.gz" "data/$WorldName/level.dat" "data/$WorldName/region" "data/$WorldName/datapacks"
docker compose exec -T mc rcon-cli save-on || true
find backups -name 'world-*.tar.gz' -mtime +$KeepDays -delete
ls -lh backups | tail -5
"@

& ssh @sshArgs $target $script
if ($LASTEXITCODE -ne 0) { throw "backup failed" }
Write-Host "backup done"
