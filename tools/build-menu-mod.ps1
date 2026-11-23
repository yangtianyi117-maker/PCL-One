<#
  Build the in-game menu mod (Fabric, client only) with plain javac + jar.
  No Gradle, no network: Minecraft 26.3 ships with official class names, and the
  Fabric API modules we need are inside the fabric-api jar already in mods/.

  Output: vendor/earth/mods/pclone-menu-<version>.jar
#>
[CmdletBinding()]
param(
    [string]$Root = "",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
if (-not $Root) { $Root = Split-Path -Parent $PSScriptRoot }

Add-Type -AssemblyName System.IO.Compression.FileSystem

$jdk = Join-Path $Root "vendor\pcl-pro\PCLProRuntime\PCL\runtime\jdk-25.0.5+8"
$javac = Join-Path $jdk "bin\javac.exe"
$jarTool = Join-Path $jdk "bin\jar.exe"
foreach ($tool in @($javac, $jarTool)) { if (-not (Test-Path $tool)) { throw "missing JDK tool: $tool" } }

$mc = Join-Path $Root "vendor\earth\game\versions\26.3\26.3.jar"
$brigadier = Get-ChildItem (Join-Path $Root "vendor\earth\game\libraries\com\mojang\brigadier") -Recurse -Filter "brigadier-*.jar" | Select-Object -First 1
$loader = Get-ChildItem (Join-Path $Root "vendor\earth\game\libraries\net\fabricmc\fabric-loader") -Recurse -Filter "fabric-loader-*.jar" | Select-Object -First 1
$api = Get-ChildItem (Join-Path $Root "vendor\earth\game\versions\Earth-26.3-Fabric\mods") -Filter "fabric-api-*.jar" | Select-Object -First 1
foreach ($p in @($mc, $brigadier.FullName, $loader.FullName, $api.FullName)) { if (-not $p -or -not (Test-Path $p)) { throw "missing dependency: $p" } }

$tmp = Join-Path $env:TEMP ("pcl-one-menu-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $tmp, (Join-Path $tmp "api"), (Join-Path $tmp "classes") | Out-Null

# nested Fabric API modules needed at compile time
$zip = [System.IO.Compression.ZipFile]::OpenRead($api.FullName)
try {
    foreach ($entry in $zip.Entries) {
        if ($entry.FullName -notlike "META-INF/jars/*.jar") { continue }
        $name = Split-Path $entry.FullName -Leaf
        if ($name -like "fabric-api-base-*" -or $name -like "fabric-screen-api-v1-*") {
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path (Join-Path $tmp "api") $name), $true)
        }
    }
} finally { $zip.Dispose() }

$base = Get-ChildItem (Join-Path $tmp "api") -Filter "fabric-api-base-*.jar" | Select-Object -First 1
$screen = Get-ChildItem (Join-Path $tmp "api") -Filter "fabric-screen-api-v1-*.jar" | Select-Object -First 1
if (-not $base -or -not $screen) { throw "fabric-api nested modules not found in $($api.Name)" }

$classpath = (@($mc, $brigadier.FullName, $loader.FullName, $base.FullName, $screen.FullName) -join ";")
$sources = @(Get-ChildItem (Join-Path $Root "menu\src") -Recurse -Filter *.java | ForEach-Object { $_.FullName })

& $javac --release 21 -encoding UTF-8 -nowarn -cp $classpath -d (Join-Path $tmp "classes") @sources
if ($LASTEXITCODE -ne 0) { throw "javac failed" }

Copy-Item (Join-Path $Root "menu\src\fabric.mod.json") (Join-Path $tmp "classes\fabric.mod.json") -Force

$outJar = Join-Path $Root ("vendor\earth\mods\pclone-menu-{0}.jar" -f $Version)
if (Test-Path $outJar) { Remove-Item $outJar -Force }
& $jarTool --create --file $outJar -C (Join-Path $tmp "classes") .
if ($LASTEXITCODE -ne 0) { throw "jar failed" }

# sanity: entrypoint + class present
$check = [System.IO.Compression.ZipFile]::OpenRead($outJar)
try {
    $names = @($check.Entries | ForEach-Object { $_.FullName })
    if ($names -notcontains "fabric.mod.json") { throw "jar missing fabric.mod.json" }
    if ($names -notcontains "pcl/one/PclOneMod.class") { throw "jar missing PclOneMod.class" }
    if ($names -notcontains "pcl/one/PclOneScreen.class") { throw "jar missing PclOneScreen.class" }
} finally { $check.Dispose() }

Remove-Item $tmp -Recurse -Force
Write-Host ("menu mod: {0} ({1:N0} bytes)" -f $outJar, (Get-Item $outJar).Length)
