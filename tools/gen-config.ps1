<#
  Single source of truth generator.
  Reads pcl-one.json and renders:
    src/generated/OfficialServers.java   -> copied into the mobile app before build
    src/generated/servers.json           -> server control plane
    src/generated/earth.html             -> public guide
    src/generated/mods.json              -> Earth client payload
    src/generated/quickplay.json         -> Earth installer defaults
#>
[CmdletBinding()]
param(
    [string]$Root = ""
)

$ErrorActionPreference = "Stop"
if (-not $Root) { $Root = Split-Path -Parent $PSScriptRoot }
$cfgPath = Join-Path $Root "pcl-one.json"
if (-not (Test-Path $cfgPath)) { throw "missing pcl-one.json: $cfgPath" }
$cfg = Get-Content $cfgPath -Raw -Encoding UTF8 | ConvertFrom-Json

$gen = Join-Path $Root "src\generated"
New-Item -ItemType Directory -Force -Path $gen | Out-Null

$map = @{
    "{{PRODUCT}}"       = [string]$cfg.product
    "{{EARTH_NAME}}"    = [string]$cfg.earth.name
    "{{EARTH_SUBTITLE}}"= [string]$cfg.earth.subtitle
    "{{HOST}}"          = [string]$cfg.earth.host
    "{{JAVA_PORT}}"     = [string]$cfg.earth.javaPort
    "{{BEDROCK_PORT}}"  = [string]$cfg.earth.bedrockPort
    "{{GUIDE_URL}}"     = [string]$cfg.earth.guideUrl
    "{{INSTANCE}}"      = [string]$cfg.earth.instance
    "{{SERVER_NAME}}"   = [string]$cfg.earth.serverName
    "{{MC}}"            = [string]$cfg.earth.mc
    "{{OFFLINE}}"       = $(if ($cfg.PSObject.Properties.Name -contains "offline" -and $cfg.offline) { "true" } else { "false" })
    "{{SPAWN_X}}"       = [string]$cfg.earth.spawn.x
    "{{SPAWN_Y}}"       = [string]$cfg.earth.spawn.y
    "{{SPAWN_Z}}"       = [string]$cfg.earth.spawn.z
}

function Render([string]$templatePath, [string]$outPath) {
    $text = [System.IO.File]::ReadAllText($templatePath)
    foreach ($k in $map.Keys) { $text = $text.Replace($k, $map[$k]) }
    [System.IO.File]::WriteAllText($outPath, $text, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ("generated {0}" -f $outPath)
}

$javaTpl = Join-Path $Root "src\mobile\OfficialServers.java.tpl"
$javaOut = Join-Path $gen "OfficialServers.java"
Render $javaTpl $javaOut
$javaDst = Join-Path $Root "src\mobile\app\src\com\pclpro\mobile\OfficialServers.java"
Copy-Item $javaOut $javaDst -Force

$htmlTpl = Join-Path $Root "src\earth\server\control\earth.html.tpl"
if (Test-Path $htmlTpl) {
    Render $htmlTpl (Join-Path $gen "earth.html")
    # Offline mobile guide: bundled into the APK assets, no URL needed.
    $mobileAssets = Join-Path $Root "src\mobile\app\assets"
    New-Item -ItemType Directory -Force -Path $mobileAssets | Out-Null
    Copy-Item (Join-Path $gen "earth.html") (Join-Path $mobileAssets "earth.html") -Force
}

$servers = [pscustomobject]@{
    servers = @(
        [pscustomobject]@{
            id          = "earth"
            name        = [string]$cfg.earth.name
            subtitle    = [string]$cfg.earth.subtitle
            description = [string]$cfg.earth.description
            tags        = @($cfg.earth.tags)
            mc          = [string]$cfg.earth.mc
            loader      = "Vanilla"
            offline     = $(if ($cfg.PSObject.Properties.Name -contains "offline" -and $cfg.offline) { $true } else { $false })
            java        = ("{0}:{1}" -f $cfg.earth.host, $cfg.earth.javaPort)
            bedrock     = ("{0}:{1}" -f $cfg.earth.host, $cfg.earth.bedrockPort)
            source      = "PCL One"
            icon        = ("http://{0}/pclpro/icon-earth.png" -f $cfg.earth.host)
        }
    )
}
$servers | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $gen "servers.json") -Encoding UTF8

Copy-Item (Join-Path $Root "shared\mods.json") (Join-Path $gen "mods.json") -Force

$quick = [pscustomobject]@{
    host       = [string]$cfg.earth.host
    javaPort   = [int]$cfg.earth.javaPort
    bedrockPort= [int]$cfg.earth.bedrockPort
    instance   = [string]$cfg.earth.instance
    serverName = [string]$cfg.earth.serverName
    mc         = [string]$cfg.earth.mc
    offline    = $(if ($cfg.PSObject.Properties.Name -contains "offline" -and $cfg.offline) { $true } else { $false })
    gameDir    = [string]$cfg.earth.gameDir
    map        = [pscustomobject]@{
        scale = "1:1000"
        size = "39936x31104"
        bounds = "-19968,-15360 .. 19968,15744"
        projection = "WebMercator"
        seaLevel = 62
    }
}
$quick | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $gen "quickplay.json") -Encoding UTF8

# Single-file offline installer metadata (compiled into the SFX bootstrap).
$sfx = @"
namespace PCLOneSfx
{
    internal static class SfxInfo
    {
        public const string Product = "$($cfg.product)";
        public const string Version = "$($cfg.version)";
        public const string RootFolder = "$($cfg.product)";
    }
}
"@
[System.IO.File]::WriteAllText((Join-Path $gen "SfxInfo.cs"), $sfx, (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("generated {0}" -f (Join-Path $gen "SfxInfo.cs"))

Write-Host ("config generated under {0}" -f $gen)
