<#
.SYNOPSIS
  Install the PCL Earth client profile into an existing PCL Pro + Minecraft setup.

.DESCRIPTION
  Does NOT touch PCLPro.exe. It only uses public data surfaces:
    - versions/<instance>/*.json  (quickPlay argument + id)
    - versions/<instance>/mods    (local payload/mods, zero network)
    - servers.dat                 (gzip NBT, merged via New-ServersDat.ps1)
    - PCL.ini                     (selected instance)
    - PCL Earth.bat               (launcher shortcut)

  OFFLINE BY DEFAULT: the eight mod jars ship in payload/mods and are copied,
  not downloaded. Pass -AllowDownload to fall back to Modrinth when a jar is
  missing from the payload.

  Every touched file is backed up under <GameDir>\pcl-earth\backup-<ts>\.
  Re-running is safe: the target instance is rebuilt from the source instance.

  NOTE: ASCII-only on purpose (Windows PowerShell 5.1 reads BOM-less .ps1 as ANSI).
#>
[CmdletBinding()]
param(
    [string]$PclRoot = "",
    [string]$GameDir = "",
    [string]$ServerHost = "124.221.231.213",
    [int]$JavaPort = 25566,
    [int]$BedrockPort = 19132,
    [string]$InstanceName = "Earth-26.3-Fabric",
    [string]$SourceInstance = "26.3-Fabric",
    [string]$ServerName = "Earth 1:1000",
    [switch]$SkipMods,
    [switch]$AllowDownload,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$script:ExitCode = 0

# Defaults come from the generated payload (single source: pcl-one.json).
$quickCfg = Join-Path $PSScriptRoot "payload\quickplay.json"
if (Test-Path $quickCfg) {
    try {
        $q = Get-Content $quickCfg -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not $PSBoundParameters.ContainsKey("ServerHost") -and $q.host) { $ServerHost = [string]$q.host }
        if (-not $PSBoundParameters.ContainsKey("JavaPort") -and $q.javaPort) { $JavaPort = [int]$q.javaPort }
        if (-not $PSBoundParameters.ContainsKey("BedrockPort") -and $q.bedrockPort) { $BedrockPort = [int]$q.bedrockPort }
        if (-not $PSBoundParameters.ContainsKey("InstanceName") -and $q.instance) { $InstanceName = [string]$q.instance }
        if (-not $PSBoundParameters.ContainsKey("ServerName") -and $q.serverName) { $ServerName = [string]$q.serverName }
    } catch {
        Write-Warning "payload/quickplay.json parse failed, using built-in defaults: $($_.Exception.Message)"
    }
}

function Fail([string]$message, [int]$code) {
    Write-Host "[install][ERROR] $message" -ForegroundColor Red
    exit $code
}

function Write-Step([string]$message) {
    Write-Host "[install] $message"
}

# --------------------------------------------------------------------------- #
# 0. resolve paths
# --------------------------------------------------------------------------- #

if (-not $GameDir) {
    # Portable package layout: EarthExtension/ next to Earth/.minecraft
    $portable = Join-Path (Split-Path -Parent $PSScriptRoot) "Earth\.minecraft"
    if (Test-Path (Join-Path $portable "versions")) {
        $GameDir = $portable
    } else {
        $GameDir = Join-Path $env:APPDATA ".minecraft"
    }
}
if (-not (Test-Path $GameDir)) { Fail "GameDir not found: $GameDir" 2 }

if (-not $PclRoot) {
    $packageRoot = Split-Path -Parent $PSScriptRoot
    $candidates = @(
        (Join-Path $packageRoot "Earth\PCLPro"),
        (Join-Path $PSScriptRoot "..\..\..\vendor\pcl-pro"),
        (Join-Path ([Environment]::GetFolderPath("Desktop")) "PCL Pro"),
        (Join-Path $env:USERPROFILE "Desktop\PCL Pro")
    )
    foreach ($c in $candidates) {
        if (Test-Path (Join-Path $c "PCLPro.exe")) { $PclRoot = $c; break }
    }
}
if (-not $PclRoot -or -not (Test-Path (Join-Path $PclRoot "PCLPro.exe"))) {
    Fail "PCLPro.exe not found. Pass -PclRoot <folder containing PCLPro.exe>." 2
}

$sourceDir = Join-Path $GameDir "versions\$SourceInstance"
$sourceJson = Join-Path $sourceDir "$SourceInstance.json"
if (-not (Test-Path $sourceJson)) { Fail "Source instance not found: $sourceJson" 2 }

$targetDir = Join-Path $GameDir "versions\$InstanceName"
$targetJson = Join-Path $targetDir "$InstanceName.json"
$serversDat = Join-Path $GameDir "servers.dat"
$pclIni = Join-Path $GameDir "PCL.ini"
$earthDir = Join-Path $GameDir "pcl-earth"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupDir = Join-Path $earthDir "backup-$stamp"
$asideDir = Join-Path $earthDir "reinstall-$stamp"
$manifestPath = Join-Path $earthDir "manifest.json"

# Reuse the original backup on reinstall; never overwrite it with an already-patched state.
$previousManifest = $null
if (Test-Path $manifestPath) {
    try { $previousManifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json } catch { }
}
if ($previousManifest -and $previousManifest.backupDir -and (Test-Path $previousManifest.backupDir)) {
    $backupDir = $previousManifest.backupDir
    Write-Step "reusing original backup: $backupDir"
}

Write-Step "PclRoot  = $PclRoot"
Write-Step "GameDir  = $GameDir"
Write-Step "Instance = $InstanceName (from $SourceInstance)"
Write-Step "Server   = $ServerHost`:$JavaPort / bedrock $BedrockPort"
if ($DryRun) { Write-Step "DryRun: no writes, no downloads" }

# --------------------------------------------------------------------------- #
# 1. backup
# --------------------------------------------------------------------------- #

if (-not $DryRun) {
    if (Test-Path $backupDir) {
        Write-Step "keeping original backups in $backupDir"
    } else {
        New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
        foreach ($f in @($pclIni, $serversDat, $sourceJson)) {
            if (Test-Path $f) {
                $dest = Join-Path $backupDir (Split-Path $f -Leaf)
                Copy-Item $f $dest -Force
                Write-Step "backup: $f -> $dest"
            }
        }
    }
}

# --------------------------------------------------------------------------- #
# 2. build instance
# --------------------------------------------------------------------------- #

function Copy-Instance {
    if (Test-Path $targetDir) {
        if (-not $DryRun) {
            New-Item -ItemType Directory -Force -Path $asideDir | Out-Null
            $old = Join-Path $asideDir $InstanceName
            Move-Item $targetDir $old -Force
            Write-Step "existing instance moved to $old"
        }
    }
    if (-not $DryRun) {
        Copy-Item $sourceDir $targetDir -Recurse -Force
        Write-Step "copied $sourceDir -> $targetDir"
    } else {
        Write-Step "would copy $sourceDir -> $targetDir"
    }
}

function Rename-VersionFiles {
    $srcJar = Join-Path $targetDir "$SourceInstance.jar"
    $dstJar = Join-Path $targetDir "$InstanceName.jar"
    if (Test-Path $srcJar) {
        if (-not $DryRun) { Move-Item $srcJar $dstJar -Force }
        Write-Step "jar: $SourceInstance.jar -> $InstanceName.jar"
    }
    $srcJson = Join-Path $targetDir "$SourceInstance.json"
    $dstJson = Join-Path $targetDir "$InstanceName.json"
    if (Test-Path $srcJson) {
        if (-not $DryRun) { Move-Item $srcJson $dstJson -Force }
        Write-Step "json: $SourceInstance.json -> $InstanceName.json"
    }
}

function Set-QuickPlay {
    param([string]$JsonPath, [string]$Address, [string]$Instance, [string]$SourceId)
    $text = [System.IO.File]::ReadAllText($JsonPath)
    # drop any previous quickPlay pair (idempotent)
    $text = [regex]::Replace($text, '\s*"--quickPlayMultiplayer"\s*,\s*"[^"]*"\s*,?', '')
    # only the root id equals the source version id; library ids are numeric/other
    $idPattern = '"id"\s*:\s*"' + [regex]::Escape($SourceId) + '"'
    $re = New-Object System.Text.RegularExpressions.Regex($idPattern)
    $text = $re.Replace($text, ('"id": "' + $Instance + '"'), 1)

    $m = [regex]::Match($text, '"game"\s*:\s*\[')
    if (-not $m.Success) { throw "no arguments.game array in $JsonPath" }
    $insertAt = $m.Index + $m.Length
    $snippet = "`r`n      ""--quickPlayMultiplayer"", ""$Address"","
    $text = $text.Insert($insertAt, $snippet)

    $obj = $text | ConvertFrom-Json
    if ($obj.id -ne $Instance) { throw "id patch failed (got '$($obj.id)')" }
    $args = @($obj.arguments.game)
    $qp = @($args | Where-Object { $_ -eq "--quickPlayMultiplayer" })
    if ($qp.Count -ne 1) { throw "quickPlay count = $($qp.Count)" }
    $idx = [Array]::IndexOf($args, "--quickPlayMultiplayer")
    if ($args[$idx + 1] -ne $Address) { throw "quickPlay address mismatch" }

    if (-not $DryRun) { [System.IO.File]::WriteAllText($JsonPath, $text, (New-Object System.Text.UTF8Encoding($false))) }
    Write-Step "patched $JsonPath"
}

Copy-Instance
Rename-VersionFiles
if (-not $DryRun) {
    Set-QuickPlay -JsonPath $targetJson -Address "$ServerHost`:$JavaPort" -Instance $InstanceName -SourceId $SourceInstance
} else {
    Write-Step "would patch $targetJson with quickPlay $ServerHost`:$JavaPort"
}

# --------------------------------------------------------------------------- #
# 3. mods
# --------------------------------------------------------------------------- #

function Get-Sha1([string]$path) {
    return (Get-FileHash -Path $path -Algorithm SHA1).Hash.ToLowerInvariant()
}

function Install-Mods {
    param([string]$InstanceDir, [string]$ManifestDir)
    if ($SkipMods) { Write-Step "SkipMods: mods not touched"; return @() }

    $modsJsonPath = Join-Path $PSScriptRoot "payload\mods.json"
    if (-not (Test-Path $modsJsonPath)) { throw "missing payload/mods.json" }
    $spec = Get-Content $modsJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($spec.mc -ne "26.3") { throw "mods.json mc mismatch: $($spec.mc)" }

    # Offline payload: shipped jars, verified by sha1. No network unless asked.
    $payloadMods = Join-Path $PSScriptRoot "payload\mods"

    $modsDir = Join-Path $InstanceDir "mods"
    if (-not $DryRun) { New-Item -ItemType Directory -Force -Path $modsDir | Out-Null }
    $installed = New-Object System.Collections.ArrayList

    foreach ($mod in $spec.mods) {
        $slug = [string]$mod.slug
        $fileName = [string]$mod.file
        $expectedSha1 = ([string]$mod.sha1).ToLowerInvariant()

        $existing = @()
        if ($fileName) {
            if (Test-Path $modsDir) { $existing = @(Get-ChildItem $modsDir -Filter "*.jar" | Where-Object { $_.Name -eq $fileName }) }
        } else {
            if (Test-Path $modsDir) { $existing = @(Get-ChildItem $modsDir -Filter "*.jar" | Where-Object { $_.Name -like "$slug*" -or $_.Name -like "*$slug*" }) }
        }
        if ($existing.Count -gt 0) {
            Write-Step ("mod {0}: already present ({1})" -f $slug, $existing[0].Name)
            [void]$installed.Add([pscustomobject]@{ slug = $slug; version_id = [string]$mod.version_id; file = $existing[0].Name })
            continue
        }

        if ($fileName -and (Test-Path (Join-Path $payloadMods $fileName))) {
            $srcJar = Join-Path $payloadMods $fileName
            $actual = Get-Sha1 $srcJar
            if ($expectedSha1 -and $actual -ne $expectedSha1) {
                throw "offline mod sha1 mismatch: $fileName (expected $expectedSha1, got $actual)"
            }
            if (-not $DryRun) { Copy-Item $srcJar (Join-Path $modsDir $fileName) -Force }
            Write-Step ("mod {0}: {1} (offline payload)" -f $slug, $fileName)
            [void]$installed.Add([pscustomobject]@{ slug = $slug; version_id = [string]$mod.version_id; file = $fileName; sha1 = $actual })
            continue
        }

        if (-not $AllowDownload) {
            throw ("mod {0} is not in payload/mods ({1}). Rebuild the offline package, or pass -AllowDownload." -f $slug, $fileName)
        }
        if ($DryRun) { Write-Step "mod ${slug}: would resolve and download"; continue }

        $gv = [uri]::EscapeDataString('["26.3"]')
        $ld = [uri]::EscapeDataString('["fabric"]')
        $url = "https://api.modrinth.com/v2/project/$slug/version?game_versions=$gv&loaders=$ld"
        try {
            $versions = Invoke-RestMethod -Uri $url -Headers @{ "User-Agent" = "PCL-Earth/1.0 (client installer)" } -TimeoutSec 30
        } catch {
            throw "Modrinth query failed for ${slug}: $($_.Exception.Message)"
        }
        $pick = @($versions)[0]
        if (-not $pick -or -not $pick.files) { throw "no 26.3 fabric build for $slug" }
        $file = @($pick.files | Where-Object { $_.primary })[0]
        if (-not $file) { $file = @($pick.files)[0] }
        $dest = Join-Path $modsDir $file.filename
        Write-Step ("mod {0}: {1} ({2} bytes, download)" -f $slug, $pick.version_number, $file.size)
        Invoke-WebRequest -Uri $file.url -OutFile $dest -UseBasicParsing -TimeoutSec 120
        [void]$installed.Add([pscustomobject]@{ slug = $slug; version_id = $pick.id; file = $file.filename; sha1 = $file.hashes.sha1 })
    }
    return $installed
}

$mods = @()
try {
    $mods = Install-Mods -InstanceDir $targetDir -ManifestDir $earthDir
} catch {
    Fail "mod install failed: $($_.Exception.Message)" 4
}

# --------------------------------------------------------------------------- #
# 4. servers.dat + PCL.ini
# --------------------------------------------------------------------------- #

$serversArgs = @(
    "-NoProfile", "-ExecutionPolicy", "Bypass",
    "-File", (Join-Path $PSScriptRoot "New-ServersDat.ps1"),
    "-Path", $serversDat, "-Name", $ServerName, "-Address", "$ServerHost`:$JavaPort"
)
if ($DryRun) {
    Write-Step "would call New-ServersDat.ps1 for $ServerName -> $ServerHost`:$JavaPort"
} else {
    & powershell @serversArgs
    if ($LASTEXITCODE -ne 0) { Fail "servers.dat write failed" 3 }
}

if (-not $DryRun) {
    $ini = @()
    if (Test-Path $pclIni) { $ini = @(Get-Content $pclIni) }
    $found = $false
    for ($i = 0; $i -lt $ini.Count; $i++) {
        if ($ini[$i] -match '^Version:') { $ini[$i] = "Version:$InstanceName"; $found = $true }
    }
    if (-not $found) { $ini += "Version:$InstanceName" }
    [System.IO.File]::WriteAllLines($pclIni, $ini, (New-Object System.Text.UTF8Encoding($false)))
    Write-Step "PCL.ini Version -> $InstanceName"
}

# --------------------------------------------------------------------------- #
# 5. shortcut
# --------------------------------------------------------------------------- #

$bat = Join-Path $PclRoot "PCL Earth.bat"
if (-not $DryRun) {
    $batText = "@echo off`r`nstart `"`" `"%~dp0PCLPro.exe`"`r`n"
    [System.IO.File]::WriteAllText($bat, $batText, (New-Object System.Text.ASCIIEncoding))
    Write-Step "shortcut: $bat"
}

# --------------------------------------------------------------------------- #
# 6. manifest
# --------------------------------------------------------------------------- #

if (-not $DryRun) {
    $manifest = [pscustomobject]@{
        version      = 1
        installedAt  = (Get-Date).ToString("o")
        pclRoot      = $PclRoot
        gameDir      = $GameDir
        instance     = $InstanceName
        source       = $SourceInstance
        server       = "$ServerHost`:$JavaPort"
        bedrock      = "$ServerHost`:$BedrockPort"
        backupDir    = $backupDir
        mods         = @($mods)
        serversDat   = $serversDat
        pclIni       = $pclIni
        shortcut     = $bat
    }
    New-Item -ItemType Directory -Force -Path $earthDir | Out-Null
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -Path $manifestPath -Encoding UTF8
    Write-Step "manifest: $manifestPath"
}

Write-Step "DONE. Launch with '$bat' or PCL Pro; the game should auto-join $ServerHost`:$JavaPort."
exit 0
