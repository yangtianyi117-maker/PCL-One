<#
  Assemble the portable, fully offline Earth game payload.

  What it produces (default: vendor/earth/game):
    assets/                     -> asset index + every object the index needs
    libraries/                  -> every library (all platforms kept, ~100 MB)
    versions/26.3/              -> vanilla json + client jar (parent version)
    versions/Earth-26.3-Fabric/ -> patched instance json + jar + mods + options
    launcher_profiles.json
    servers.dat                 -> Earth entry, so multiplayer already has it
    OFFLINE.json                -> what was assembled, sizes, hashes

  The payload is staged by build.ps1 into dist/PCL One/Earth/.minecraft and the
  portable PCL Pro config points at it. No installer, no download, no network.

  Usage:
    .\prepare-earth-offline.ps1                        # from %APPDATA%\.minecraft
    .\prepare-earth-offline.ps1 -SourceGameDir D:\mc   # from another game dir
    .\prepare-earth-offline.ps1 -RefreshMods           # re-resolve the mod set
    .\prepare-earth-offline.ps1 -Validate              # verify an existing payload

  NOTE: ASCII-only (Windows PowerShell 5.1 reads BOM-less .ps1 as ANSI).
#>
[CmdletBinding()]
param(
    [string]$Root = "",
    [string]$SourceGameDir = "",
    [string]$SourceInstance = "26.3-Fabric",
    [string]$TargetInstance = "Earth-26.3-Fabric",
    [string]$OutDir = "",
    [switch]$RefreshMods,
    [switch]$Validate,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
if (-not $Root) { $Root = Split-Path -Parent $PSScriptRoot }

$cfg = Get-Content (Join-Path $Root "pcl-one.json") -Raw -Encoding UTF8 | ConvertFrom-Json
$mcVersion = [string]$cfg.earth.mc
if (-not $mcVersion) { $mcVersion = "26.3" }
$serverHost = [string]$cfg.earth.host
$javaPort = [int]$cfg.earth.javaPort
$serverName = [string]$cfg.earth.serverName

if (-not $OutDir) { $OutDir = Join-Path $Root "vendor\earth\game" }
$modsDir = Join-Path $Root "vendor\earth\mods"
$manifestPath = Join-Path $Root "shared\mods.json"
$serversDatScript = Join-Path $Root "src\earth\client\New-ServersDat.ps1"

function Step([string]$m) { Write-Host ("==> {0}" -f $m) }
function Ensure([string]$p) { if (-not (Test-Path $p)) { throw "missing: $p" } }

function Copy-Tree([string]$src, [string]$dst) {
    Ensure $src
    New-Item -ItemType Directory -Force -Path $dst | Out-Null
    robocopy $src $dst /E /NFL /NDL /NJH /NJS /NP /R:2 /W:2 | Out-Null
    if ($LASTEXITCODE -ge 8) { throw ("robocopy failed ({0}): {1} -> {2}" -f $LASTEXITCODE, $src, $dst) }
    $global:LASTEXITCODE = 0
}

# Mirrors PCL's Libraries.McJsonRuleCheck for a Windows x64 host.
function Test-WindowsRules($rules) {
    if ($null -eq $rules) { return $true }
    $required = $false
    foreach ($rule in @($rules)) {
        $osMatch = $true
        if ($rule.os) {
            if ($rule.os.name -and $rule.os.name -ne "windows") { $osMatch = $false }
            if ($rule.os.arch -and $rule.os.arch -eq "x86") { $osMatch = $false }
        }
        $featuresMatch = $true
        if ($rule.features) {
            foreach ($p in $rule.features.PSObject.Properties) {
                if ($p.Name -like "*quick_play*") { $featuresMatch = $false }
            }
        }
        $isRight = $osMatch -and $featuresMatch
        if ($rule.action -eq "allow") {
            if ($isRight) { $required = $true }
        } elseif ($isRight) {
            $required = $false
        }
    }
    return $required
}

function Test-Payload([string]$gameDir) {
    Step "validate payload $gameDir"
    $errors = New-Object System.Collections.ArrayList

    $instDir = Join-Path $gameDir ("versions\" + $TargetInstance)
    $instJson = Join-Path $instDir ($TargetInstance + ".json")
    if (-not (Test-Path $instJson)) { [void]$errors.Add("missing instance json: $instJson"); return $errors }

    $json = Get-Content $instJson -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($json.id -ne $TargetInstance) { [void]$errors.Add("instance id is not $TargetInstance") }

    # parent jar
    $parentJar = Join-Path $gameDir ("versions\{0}\{0}.jar" -f $mcVersion)
    if (-not (Test-Path $parentJar)) { [void]$errors.Add("missing parent jar: $parentJar") }

    # libraries PCL would actually use on Windows x64
    $libCount = 0
    $libMissing = 0
    foreach ($lib in $json.libraries) {
        if (-not (Test-WindowsRules $lib.rules)) { continue }
        $rel = $null
        if ($lib.downloads -and $lib.downloads.artifact) { $rel = $lib.downloads.artifact.path }
        if (-not $rel) {
            $parts = ([string]$lib.name).Split(":")
            $rel = ($parts[0] -replace "\.", "\") + "\" + $parts[1] + "\" + $parts[2] + "\" + $parts[1] + "-" + $parts[2] + ".jar"
        }
        $p = Join-Path (Join-Path $gameDir "libraries") $rel
        $libCount++
        if (-not (Test-Path $p)) { $libMissing++; if ($libMissing -le 5) { [void]$errors.Add("missing library: $rel") } }
    }
    if ($libMissing -gt 5) { [void]$errors.Add("... and $($libMissing - 5) more missing libraries") }

    # assets
    $assetIndexId = $null
    if ($json.assetIndex) { $assetIndexId = [string]$json.assetIndex.id }
    if (-not $assetIndexId) { [void]$errors.Add("instance json has no assetIndex") }
    else {
        $idxPath = Join-Path $gameDir ("assets\indexes\" + $assetIndexId + ".json")
        if (-not (Test-Path $idxPath)) { [void]$errors.Add("missing asset index: $idxPath") }
        else {
            $idx = Get-Content $idxPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $objCount = 0
            $objMissing = 0
            foreach ($o in $idx.objects.PSObject.Properties) {
                $h = [string]$o.Value.hash
                $op = Join-Path $gameDir ("assets\objects\" + $h.Substring(0, 2) + "\" + $h)
                $objCount++
                if (-not (Test-Path $op)) { $objMissing++ }
            }
            if ($objMissing -gt 0) { [void]$errors.Add("$objMissing of $objCount asset objects are missing") }
        }
    }

    # mods
    $modsPath = Join-Path $instDir "mods"
    $expected = @()
    if (Test-Path $manifestPath) { $expected = @((Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json).mods) }
    $present = @(Get-ChildItem $modsPath -Filter *.jar -ErrorAction SilentlyContinue)
    if ($present.Count -lt $expected.Count) {
        [void]$errors.Add("mods: {0} present, {1} expected" -f $present.Count, $expected.Count)
    }
    foreach ($m in $expected) {
        if (-not (Test-Path (Join-Path $modsPath ([string]$m.file)))) { [void]$errors.Add("missing mod: $($m.file)") }
    }

    # java runtime shipped with PCL Pro
    $java = Join-Path $Root "vendor\pcl-pro\PCLProRuntime\PCL\runtime"
    $jdk = @(Get-ChildItem $java -Directory -Filter "jdk-*" -ErrorAction SilentlyContinue | Where-Object { Test-Path (Join-Path $_.FullName "bin\java.exe") })
    if ($jdk.Count -eq 0) { [void]$errors.Add("no bundled JDK under $java") }

    if ($errors.Count -eq 0) {
        $sum = (Get-ChildItem $gameDir -Recurse -File | Measure-Object Length -Sum)
        Write-Host ("    OK  {0:N1} MB / {1} files   ({2} libs, {3} mods)" -f ($sum.Sum / 1MB), $sum.Count, $libCount, $present.Count)
    } else {
        foreach ($e in $errors) { Write-Host ("    FAIL {0}" -f $e) }
    }
    return $errors
}

if ($Validate) {
    $errs = Test-Payload $OutDir
    if (@($errs).Count -gt 0) { exit 1 }
    exit 0
}

# --------------------------------------------------------------------------- #
# 1. mods (offline, pinned)
# --------------------------------------------------------------------------- #
Step "pin mod set"
$fetchArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $Root "tools\fetch-mods.ps1"), "-Root", $Root, "-GameVersion", $mcVersion)
if ($RefreshMods) { $fetchArgs += "-Force" }
& powershell @fetchArgs
if ($LASTEXITCODE -ne 0) { throw "fetch-mods failed" }
Ensure $manifestPath
Ensure $modsDir

# --------------------------------------------------------------------------- #
# 2. source game dir
# --------------------------------------------------------------------------- #
if (-not $SourceGameDir) { $SourceGameDir = Join-Path $env:APPDATA ".minecraft" }
Ensure $SourceGameDir

$srcInstanceDir = Join-Path $SourceGameDir ("versions\" + $SourceInstance)
$srcJsonPath = Join-Path $srcInstanceDir ($SourceInstance + ".json")
Ensure $srcJsonPath
$srcRootJson = Get-Content $srcJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
$assetIndexId = [string]$srcRootJson.assetIndex.id
Ensure (Join-Path $SourceGameDir ("assets\indexes\" + $assetIndexId + ".json"))
Ensure (Join-Path $SourceGameDir "libraries")
Ensure (Join-Path $SourceGameDir ("versions\{0}\{0}.jar" -f $mcVersion))

if ($Clean -and (Test-Path $OutDir)) { Remove-Item $OutDir -Recurse -Force }

Step "stage assets"
Copy-Tree (Join-Path $SourceGameDir "assets") (Join-Path $OutDir "assets")

Step "stage libraries"
Copy-Tree (Join-Path $SourceGameDir "libraries") (Join-Path $OutDir "libraries")

Step "stage parent version $mcVersion"
$parentSrc = Join-Path $SourceGameDir ("versions\" + $mcVersion)
$parentDst = Join-Path $OutDir ("versions\" + $mcVersion)
New-Item -ItemType Directory -Force -Path $parentDst | Out-Null
Copy-Item (Join-Path $parentSrc ($mcVersion + ".jar")) $parentDst -Force
if (Test-Path (Join-Path $parentSrc ($mcVersion + ".json"))) {
    Copy-Item (Join-Path $parentSrc ($mcVersion + ".json")) $parentDst -Force
}

Step "build instance $TargetInstance"
$instDir = Join-Path $OutDir ("versions\" + $TargetInstance)
if (Test-Path $instDir) { Remove-Item $instDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $instDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $instDir "mods") | Out-Null

# instance json: retarget id, keep self-contained library list
$text = [System.IO.File]::ReadAllText($srcJsonPath)
$text = [regex]::Replace($text, ('\s*"--quickPlayMultiplayer"\s*,\s*"[^"]*"\s*,?'), '')
$text = [regex]::Replace($text, '\s*"--quickPlayMultiplayer"\s*,\s*"\$\{quickPlayMultiplayer\}"\s*,?', '')
$idPattern = '"id"\s*:\s*"' + [regex]::Escape($SourceInstance) + '"'
$text = (New-Object System.Text.RegularExpressions.Regex($idPattern)).Replace($text, ('"id": "' + $TargetInstance + '"'), 1)
# the parent jar carries the game classes; do not need a duplicate copy
$text = [regex]::Replace($text, '"jar"\s*:\s*"[^"]*"', ('"jar": "' + $mcVersion + '"'), 1)
$instJsonPath = Join-Path $instDir ($TargetInstance + ".json")
[System.IO.File]::WriteAllText($instJsonPath, $text, (New-Object System.Text.UTF8Encoding($false)))

# sanity: json still parses and is self-contained
$check = Get-Content $instJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($check.id -ne $TargetInstance) { throw "instance id patch failed: $($check.id)" }
if ($check.inheritsFrom) { throw "instance json unexpectedly inherits from $($check.inheritsFrom)" }

# mods
$expected = @((Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json).mods)
foreach ($m in $expected) {
    $srcJar = Join-Path $modsDir ([string]$m.file)
    Ensure $srcJar
    Copy-Item $srcJar (Join-Path $instDir "mods") -Force
}
# first-party menu mod (not in the Modrinth manifest)
Get-ChildItem $modsDir -Filter "pclone-*.jar" -ErrorAction SilentlyContinue | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $instDir "mods") -Force
}

# options.txt: friendly fresh defaults (Minecraft fills the rest)
$options = @(
    "lang:zh_cn",
    "fov:0.0",
    "renderDistance:12",
    "simulationDistance:12",
    "guiScale:0",
    "pauseOnLostFocus:false",
    "tutorialStep:none",
    "skipMultiplayerWarning:true",
    "skipRealms32bitWarning:true",
    "joinedFirstServer:true",
    "onboardAccessibility:false",
    "soundCategory_master:0.8",
    "narrator:0"
)
[System.IO.File]::WriteAllLines((Join-Path $instDir "options.txt"), $options, (New-Object System.Text.UTF8Encoding($false)))

# servers.dat: Earth is already in the list, so multiplayer works with no paste
$serversDat = Join-Path $instDir "servers.dat"
& powershell -NoProfile -ExecutionPolicy Bypass -File $serversDatScript -Path $serversDat -Name $serverName -Address ("{0}:{1}" -f $serverHost, $javaPort)
if ($LASTEXITCODE -ne 0) { throw "servers.dat write failed" }

Step "write PCL metadata"
$profiles = @'
{
  "profiles": {
    "Earth": {
      "icon": "Grass",
      "name": "Earth 1:1000",
      "lastVersionId": "Earth-26.3-Fabric",
      "type": "custom",
      "lastUsed": "2026-01-01T00:00:00.0000Z"
    }
  },
  "selectedProfile": "Earth",
  "clientToken": "23323323323323323323323323323333"
}
'@
[System.IO.File]::WriteAllText((Join-Path $OutDir "launcher_profiles.json"), $profiles, (New-Object System.Text.UTF8Encoding($false)))

$pclIni = @("Version:$TargetInstance", "InstanceCache:")
[System.IO.File]::WriteAllLines((Join-Path $OutDir "PCL.ini"), $pclIni, (New-Object System.Text.UTF8Encoding($false)))

Step "prune runtime junk"
$junkNames = @(".fabric", "logs", "crash-reports", "saves", "downloads", "data", "config", "xaero", "screenshots", "replay_recordings")
$junkDirs = @(Get-ChildItem $instDir -Directory -ErrorAction SilentlyContinue | Where-Object {
    ($junkNames -contains $_.Name) -or ($_.Name -like "*-natives")
})
foreach ($d in $junkDirs) { Remove-Item $d.FullName -Recurse -Force -ErrorAction SilentlyContinue }
$junkRoot = @(Get-ChildItem $OutDir -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -in @("logs", "crash-reports") })
foreach ($d in $junkRoot) { Remove-Item $d.FullName -Recurse -Force -ErrorAction SilentlyContinue }

# --------------------------------------------------------------------------- #
# 3. validate + summary
# --------------------------------------------------------------------------- #
$errors = Test-Payload $OutDir
if (@($errors).Count -gt 0) { throw "payload validation failed ($(@($errors).Count) problems)" }

$sum = (Get-ChildItem $OutDir -Recurse -File | Measure-Object Length -Sum)
$info = [pscustomobject]@{
    product        = [string]$cfg.product
    kind           = "offline-earth-payload"
    mc             = $mcVersion
    instance       = $TargetInstance
    sourceInstance = $SourceInstance
    sourceGameDir  = $SourceGameDir
    server         = ("{0}:{1}" -f $serverHost, $javaPort)
    bedrock        = ("{0}:{1}" -f $serverHost, [int]$cfg.earth.bedrockPort)
    built          = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    bytes          = [int64]$sum.Sum
    files          = [int]$sum.Count
    mods           = @($expected | ForEach-Object { [string]$_.file })
}
$info | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $Root "vendor\earth\OFFLINE.json") -Encoding UTF8

Write-Host ("OFFLINE EARTH OK  {0:N1} MB / {1} files" -f ($sum.Sum / 1MB), $sum.Count)
exit 0
