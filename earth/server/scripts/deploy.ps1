<#
  Deploy server files to the host. Does not download plugins automatically:
  put the jars in server/plugins/ first (see server/plugins.lock.json).

  Examples:
    .\deploy.ps1 -SshHost 124.221.231.213 -UploadConfig -Start
    .\deploy.ps1 -UploadWorld -WorldDir 'C:\Users\Administrator\Desktop\Earth1000' -Start
#>
[CmdletBinding()]
param(
    [string]$SshHost = "124.221.231.213",
    [string]$SshUser = "root",
    [int]$SshPort = 22,
    [string]$KeyFile = "",
    [string]$RemoteDir = "/srv/pcl-earth",
    [string]$WorldDir = "",
    [switch]$UploadWorld,
    [switch]$UploadConfig,
    [switch]$Start,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$sshArgs = @("-o", "BatchMode=yes", "-p", "$SshPort")
$scpArgs = @("-o", "BatchMode=yes", "-P", "$SshPort")
if ($KeyFile) { $sshArgs += @("-i", $KeyFile); $scpArgs += @("-i", $KeyFile) }
$target = "$SshUser@$SshHost"
$root = Split-Path -Parent $PSScriptRoot

function Run([string]$cmd) {
    Write-Host "ssh> $cmd"
    if (-not $DryRun) { & ssh @sshArgs $target $cmd; if ($LASTEXITCODE -ne 0) { throw "remote command failed: $cmd" } }
}

Run "mkdir -p $RemoteDir/{data,plugins,config,control,backups}"

if ($UploadConfig) {
    Write-Host "uploading compose/config..."
    if (-not $DryRun) {
        & scp @scpArgs (Join-Path $root "docker-compose.yml") "${target}:$RemoteDir/"
        & scp @scpArgs (Join-Path $root "server.properties") "${target}:$RemoteDir/"
        & scp @scpArgs (Join-Path $root "config\*") "${target}:$RemoteDir/config/"
        & scp @scpArgs (Join-Path $root "control\*") "${target}:$RemoteDir/control/"
        if (Test-Path (Join-Path $root "plugins")) {
            & scp @scpArgs -r (Join-Path $root "plugins\*") "${target}:$RemoteDir/plugins/"
        }
    }
    Run "cp $RemoteDir/server.properties $RemoteDir/data/server.properties || true"
    Run "cp $RemoteDir/config/geyser.yml $RemoteDir/plugins/Geyser-Spigot/config.yml || true"
    Run "cp $RemoteDir/config/floodgate.yml $RemoteDir/plugins/floodgate/config.yml || true"
    Run "cp $RemoteDir/config/bluemap-core.conf $RemoteDir/plugins/BlueMap/core.conf || true"
    Run "cp $RemoteDir/control/servers.json $RemoteDir/control/servers.json"
}

if ($UploadWorld) {
    if (-not $WorldDir -or -not (Test-Path (Join-Path $WorldDir "level.dat"))) {
        throw "WorldDir must contain level.dat: $WorldDir"
    }
    Write-Host "uploading world (resume with rsync)..."
    if (-not $DryRun) {
        & rsync -avP --partial "$WorldDir/" "${target}:$RemoteDir/data/Earth1000/"
        if ($LASTEXITCODE -ne 0) { throw "rsync world failed" }
    }
}

if ($Start) {
    Run "cd $RemoteDir && docker compose up -d"
    Run "cd $RemoteDir && docker compose ps"
}

Write-Host "done"
