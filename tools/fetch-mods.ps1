<#
  Fetch the Earth client mod set ONCE, pin it, and keep the jars on disk so that
  every later build and every install is 100% offline.

  Input : shared/mods.json (slugs only on first run)
  Output: vendor/earth/mods/<file>.jar
          shared/mods.json (pinned: version_id, file, size, sha1, url)

  Usage:
    .\fetch-mods.ps1                 # keep existing jars, resolve only new ones
    .\fetch-mods.ps1 -Force          # re-resolve and re-download everything
    .\fetch-mods.ps1 -Check          # verify local jars against the pinned sha1

  NOTE: ASCII-only (Windows PowerShell 5.1 reads BOM-less .ps1 as ANSI).
#>
[CmdletBinding()]
param(
    [string]$Root = "",
    [string]$GameVersion = "26.3",
    [string]$Loader = "fabric",
    [switch]$Force,
    [switch]$Check
)

$ErrorActionPreference = "Stop"
if (-not $Root) { $Root = Split-Path -Parent $PSScriptRoot }

$manifestPath = Join-Path $Root "shared\mods.json"
$modsDir = Join-Path $Root "vendor\earth\mods"
$userAgent = "PCL-One/2.0 (offline build; +https://pcl.one)"

if (-not (Test-Path $manifestPath)) { throw "missing manifest: $manifestPath" }
$spec = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $spec.mods) { throw "manifest has no mods: $manifestPath" }

if (-not (Test-Path $modsDir)) { New-Item -ItemType Directory -Force -Path $modsDir | Out-Null }

function Get-Sha1([string]$path) {
    return (Get-FileHash -Path $path -Algorithm SHA1).Hash.ToLowerInvariant()
}

function Resolve-Mod([string]$slug) {
    $gv = [uri]::EscapeDataString(('["{0}"]' -f $GameVersion))
    $ld = [uri]::EscapeDataString(('["{0}"]' -f $Loader))
    $url = "https://api.modrinth.com/v2/project/$slug/version?game_versions=$gv&loaders=$ld"
    $versions = @(Invoke-RestMethod -Uri $url -Headers @{ "User-Agent" = $userAgent } -TimeoutSec 30)
    if ($versions.Count -eq 0) { throw "no $GameVersion $Loader build for $slug" }
    $pick = $versions[0]
    $file = @($pick.files | Where-Object { $_.primary })[0]
    if (-not $file) { $file = @($pick.files)[0] }
    return [pscustomobject]@{
        slug           = $slug
        project_id     = [string]$pick.project_id
        version_id     = [string]$pick.id
        version_number = [string]$pick.version_number
        file           = [string]$file.filename
        size           = [int64]$file.size
        sha1           = ([string]$file.hashes.sha1).ToLowerInvariant()
        url            = [string]$file.url
    }
}

$out = New-Object System.Collections.ArrayList
$bad = 0
foreach ($mod in $spec.mods) {
    $slug = [string]$mod.slug
    if (-not $slug) { continue }

    $entry = [pscustomobject]@{
        slug = $slug; project_id = ""; version_id = ""; version_number = ""
        file = ""; size = 0; sha1 = ""; url = ""
    }

    $pinned = ($mod.file -and $mod.sha1)
    if ($pinned -and -not $Force) {
        $entry.project_id = [string]$mod.project_id
        $entry.version_id = [string]$mod.version_id
        $entry.version_number = [string]$mod.version_number
        $entry.file = [string]$mod.file
        $entry.size = [int64]$mod.size
        $entry.sha1 = ([string]$mod.sha1).ToLowerInvariant()
        $entry.url = [string]$mod.url
    } else {
        if ($Check) { throw "mod $slug is not pinned yet; run fetch-mods.ps1 once online" }
        Write-Host ("[mods] resolve {0} ..." -f $slug)
        $entry = Resolve-Mod $slug
    }

    $local = Join-Path $modsDir $entry.file
    $needDownload = $true
    if (Test-Path $local) {
        if ($Check) {
            $actual = Get-Sha1 $local
            if ($actual -ne $entry.sha1) {
                throw ("sha1 mismatch for {0}: expected {1}, got {2}" -f $entry.file, $entry.sha1, $actual)
            }
        }
        $needDownload = $false
        Write-Host ("[mods] cached {0}" -f $entry.file)
    }

    if ($needDownload) {
        if (-not $entry.url) { throw "mod $slug has no download url; run fetch-mods.ps1 online once" }
        if ($Check) { throw "missing jar for pinned mod ${slug}: $local" }
        Write-Host ("[mods] download {0} ({1:N0} bytes)" -f $entry.file, $entry.size)
        Invoke-WebRequest -Uri $entry.url -OutFile $local -UseBasicParsing -TimeoutSec 180 -Headers @{ "User-Agent" = $userAgent }
        $actual = Get-Sha1 $local
        if ($entry.sha1 -and $actual -ne $entry.sha1) {
            Remove-Item $local -Force
            throw ("sha1 mismatch after download for {0}: expected {1}, got {2}" -f $entry.file, $entry.sha1, $actual)
        }
        $entry.sha1 = $actual
        $entry.size = (Get-Item $local).Length
    }

    [void]$out.Add($entry)
}

$manifest = [pscustomobject]@{
    mc        = $GameVersion
    loader    = $Loader
    generated = (Get-Date).ToString("yyyy-MM-dd")
    note      = "Pinned for the PCL One offline package. Jars live in vendor/earth/mods."
    mods      = @($out)
}

if (-not $Check) {
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -Path $manifestPath -Encoding UTF8
    Write-Host ("[mods] manifest pinned: {0}" -f $manifestPath)
}

$total = ($out | Measure-Object -Property size -Sum).Sum
Write-Host ("[mods] {0} mods, {1:N1} MB" -f $out.Count, ($total / 1MB))
if ($bad -gt 0) { exit 1 }
exit 0
