<#
  Hard proof that the offline package is really offline.

  It builds a Minecraft launch command ONLY from files inside the package
  (bundled JDK + Earth/.minecraft libraries/assets/versions/mods), starts the
  game, and asserts from the game log that 26.3 + Fabric actually booted.

  No PCL Pro, no %APPDATA%, no network: if a file were missing the JVM would
  fail before printing "Loading Minecraft".

  Usage:
    .\offline-smoke.ps1                         # prefer dist/PCL One, else vendor
    .\offline-smoke.ps1 -PackageDir <dir>       # test a specific copy
    .\offline-smoke.ps1 -Seconds 40 -KeepOpen   # longer boot window / keep window open

  NOTE: ASCII-only (Windows PowerShell 5.1 reads BOM-less .ps1 as ANSI).
#>
[CmdletBinding()]
param(
    [string]$Root = "",
    [string]$PackageDir = "",
    [int]$Seconds = 40,
    [switch]$KeepOpen,
    [switch]$NoCleanup
)

$ErrorActionPreference = "Stop"
if (-not $Root) { $Root = Split-Path -Parent $PSScriptRoot }

Add-Type -AssemblyName System.IO.Compression.FileSystem

$cfg = Get-Content (Join-Path $Root "pcl-one.json") -Raw -Encoding UTF8 | ConvertFrom-Json
$instance = [string]$cfg.earth.instance
$mc = [string]$cfg.earth.mc

function First-Existing([string[]]$paths) {
    foreach ($p in $paths) { if (Test-Path $p) { return $p } }
    return $null
}

# ---------------------------------------------------------------- locate package
$distPkg = Join-Path $Root "dist\PCL One"
$pkg = $PackageDir
if (-not $pkg) {
    if (Test-Path (Join-Path $distPkg "Earth\.minecraft\versions")) { $pkg = $distPkg }
    else { $pkg = $Root }
}

$gameDir = First-Existing @(
    (Join-Path $pkg "Earth\.minecraft"),
    (Join-Path $Root "vendor\earth\game")
)
if (-not $gameDir) { throw "no Earth game dir found (looked in $pkg and vendor)" }

$pclRoots = @(
    (Join-Path $pkg "Earth\PCLPro\PCLProRuntime\PCL\runtime"),
    (Join-Path $Root "vendor\pcl-pro\PCLProRuntime\PCL\runtime")
)
$runtimeDir = First-Existing $pclRoots
if (-not $runtimeDir) { throw "no bundled Java runtime found" }
$jdk = Get-ChildItem $runtimeDir -Directory -Filter "jdk-*" |
    Where-Object { Test-Path (Join-Path $_.FullName "bin\java.exe") } |
    Select-Object -First 1
if (-not $jdk) { throw "no jdk-*/bin/java.exe under $runtimeDir" }
$java = Join-Path $jdk.FullName "bin\java.exe"

$instDir = Join-Path $gameDir ("versions\" + $instance)
$instJsonPath = Join-Path $instDir ($instance + ".json")
if (-not (Test-Path $instJsonPath)) { throw "missing instance json: $instJsonPath" }
$json = Get-Content $instJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json

Write-Host ("package : {0}" -f $pkg)
Write-Host ("game    : {0}" -f $gameDir)
Write-Host ("java    : {0}" -f $java)

# ---------------------------------------------------------------- rules + libs
function Test-Win64Rules($rules) {
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

$libJars = New-Object System.Collections.ArrayList
$nativesJars = New-Object System.Collections.ArrayList
$seen = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($lib in $json.libraries) {
    if (-not (Test-Win64Rules $lib.rules)) { continue }
    $artifactPath = $null
    if ($lib.downloads -and $lib.downloads.artifact -and $lib.downloads.artifact.path) {
        $artifactPath = [string]$lib.downloads.artifact.path
    } elseif ($lib.name) {
        # older/profile-style entries carry only coordinates
        $parts = ([string]$lib.name).Split(":")
        if ($parts.Count -ge 3) {
            $artifactPath = ($parts[0] -replace "\.", "\") + "\" + $parts[1] + "\" + $parts[2] + "\" + $parts[1] + "-" + $parts[2] + ".jar"
        }
    }
    if ($artifactPath) {
        $p = Join-Path (Join-Path $gameDir "libraries") $artifactPath
        if (-not (Test-Path $p)) { throw "missing library: $p" }
        if ($seen.Add($p)) { [void]$libJars.Add($p) }
    }
    if ($lib.downloads -and $lib.downloads.classifiers) {
        foreach ($c in $lib.downloads.classifiers.PSObject.Properties) {
            if ($c.Name -notlike "natives-windows*") { continue }
            if (-not $c.Value.path) { continue }
            $np = Join-Path (Join-Path $gameDir "libraries") ([string]$c.Value.path)
            if (Test-Path $np) { [void]$nativesJars.Add($np) }
        }
    }
}

$parentJar = Join-Path $gameDir ("versions\{0}\{0}.jar" -f $mc)
if (-not (Test-Path $parentJar)) { throw "missing parent jar: $parentJar" }
$classpath = (@($parentJar) + @($libJars)) -join ";"

# ---------------------------------------------------------------- natives
$tmp = Join-Path $env:TEMP ("pcl-one-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
$natives = Join-Path $tmp "natives"
New-Item -ItemType Directory -Force -Path $natives | Out-Null
foreach ($nj in $nativesJars) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($nj)
    try {
        foreach ($e in $zip.Entries) {
            if ($e.FullName.EndsWith("/")) { continue }
            $name = [System.IO.Path]::GetFileName($e.FullName)
            if (-not $name) { continue }
            $dest = Join-Path $natives $name
            if (-not (Test-Path $dest)) {
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($e, $dest, $true)
            }
        }
    } finally { $zip.Dispose() }
}

# ---------------------------------------------------------------- game args
$assetIndex = [string]$json.assetIndex.id
$tokens = @{
    '${auth_player_name}'   = 'PCLOneOffline'
    '${version_name}'       = $instance
    '${game_directory}'     = $instDir
    '${assets_root}'        = (Join-Path $gameDir "assets")
    '${game_assets}'        = (Join-Path $gameDir "assets\virtual\legacy")
    '${assets_index_name}'  = $assetIndex
    '${auth_uuid}'          = '75f41ffffffffffffffffffffffff867'
    '${auth_access_token}'  = '75f41ffffffffffffffffffffffff867'
    '${access_token}'       = '75f41ffffffffffffffffffffffff867'
    '${auth_session}'       = '75f41ffffffffffffffffffffffff867'
    '${clientid}'           = 'pcl-one-smoke'
    '${auth_xuid}'          = ''
    '${user_type}'          = 'legacy'
    '${version_type}'       = 'PCLOne'
    '${natives_directory}'  = $natives
    '${launcher_name}'      = 'PCLOne'
    '${launcher_version}'   = '1'
    '${classpath}'          = $classpath
    '${library_directory}'  = (Join-Path $gameDir "libraries")
    '${libraries_directory}'= (Join-Path $gameDir "libraries")
    '${resolution_width}'   = '854'
    '${resolution_height}'  = '480'
    '${user_properties}'    = '{}'
}

function Expand-Token([string]$value) {
    $result = $value
    foreach ($k in $tokens.Keys) { $result = $result.Replace($k, [string]$tokens[$k]) }
    # paths with spaces must stay a single argv entry
    if ($result.Contains(' ') -and -not $result.StartsWith('"')) { $result = '"' + $result + '"' }
    return $result
}

$gameArgs = New-Object System.Collections.ArrayList
foreach ($a in @($json.arguments.game)) {
    if ($a -is [string]) { [void]$gameArgs.Add($a); continue }
    if ($null -eq $a) { continue }
    if (-not (Test-Win64Rules $a.rules)) { continue }
    foreach ($v in @($a.value)) { [void]$gameArgs.Add([string]$v) }
}
$gameArgLine = (@($gameArgs | ForEach-Object { Expand-Token $_ }) -join " ")

$jvmFlags = @(
    '-Dfile.encoding=COMPAT',
    '-Dstderr.encoding=UTF-8',
    '-Dstdout.encoding=UTF-8',
    '-XX:+UseG1GC',
    '-XX:-UseAdaptiveSizePolicy',
    '-XX:-OmitStackTraceInFastThrow',
    '-Djdk.lang.Process.allowAmbiguousCommands=true',
    '-Dlog4j2.formatMsgNoLookups=true',
    '-DFabricMcEmu=net.minecraft.client.main.Main',
    '-Xmx2G'
)

$argLine = ($jvmFlags -join ' ') + ' "-Djava.library.path=' + $natives + '" -cp "' + $classpath + '" ' + $json.mainClass + ' ' + $gameArgLine
$logPath = Join-Path $Root ".offline-smoke.log"
if (Test-Path $logPath) { Remove-Item $logPath -Force }
$errPath = Join-Path $Root ".offline-smoke.err.log"
if (Test-Path $errPath) { Remove-Item $errPath -Force }

Write-Host "starting game from package files only (no PCL, no APPDATA, no network) ..."
$proc = Start-Process -FilePath $java -ArgumentList $argLine -PassThru -WindowStyle Hidden `
    -WorkingDirectory $instDir -RedirectStandardOutput $logPath -RedirectStandardError $errPath

$deadline = (Get-Date).AddSeconds($Seconds)
$ok = $false
$booted = $false
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 700
    if (Test-Path $logPath) {
        $text = Get-Content $logPath -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
        if ($text -and $text -match 'Loading Minecraft ' + [regex]::Escape($mc)) { $booted = $true }
        if ($text -and $text -match 'Created: .*atlas') { $ok = $true; break }
    }
    if ($proc.HasExited) { break }
}

$exitedEarly = $proc.HasExited
if (-not $KeepOpen) {
    try { if (-not $proc.HasExited) { $proc.Kill(); $proc.WaitForExit(5000) | Out-Null } } catch { }
}

# a smoke run is a test, not a playthrough: drop the folders Minecraft generated
if (-not $NoCleanup) {
    $junk = @(".fabric", "logs", "crash-reports", "saves", "downloads", "data", "config", "xaero", "screenshots")
    $targets = @(Get-ChildItem $instDir -Directory -ErrorAction SilentlyContinue | Where-Object {
        ($junk -contains $_.Name) -or ($_.Name -like "*-natives")
    })
    foreach ($d in $targets) { Remove-Item $d.FullName -Recurse -Force -ErrorAction SilentlyContinue }
    Remove-Item (Join-Path $tmp "natives") -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "----------------------------------------"
if (Test-Path $logPath) { Get-Content $logPath -Encoding UTF8 -Tail 12 }
Write-Host "----------------------------------------"

$problems = @()
if (-not (Test-Path $logPath)) { $problems += "no game log was produced" }
else {
    $text = Get-Content $logPath -Raw -Encoding UTF8
    if ($text -notmatch 'Loading Minecraft ' + [regex]::Escape($mc)) { $problems += "log never said 'Loading Minecraft $mc'" }
    if ($text -notmatch 'Created: .*atlas') { $problems += "texture atlases were never created" }
    if ($text -match '(?im)^.*(Missing|missing library|Could not find or load main class|NoClassDefFoundError).*$') {
        $problems += "log contains a missing-file/class error"
    }
}
if ($exitedEarly -and -not $ok) { $problems += "game process exited before becoming ready (code $($proc.ExitCode))" }

if ($problems.Count -eq 0) {
    Write-Host ("OFFLINE SMOKE PASS  (booted={0}, atlases={1})" -f $booted, $ok)
    Write-Host ("log: {0}" -f $logPath)
    exit 0
}
foreach ($p in $problems) { Write-Host ("FAIL: {0}" -f $p) }
if (Test-Path $errPath) { Get-Content $errPath -Encoding UTF8 -Tail 20 | Write-Host }
exit 1
