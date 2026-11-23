# Build script for PCL Pro 鎵嬫満鐗?# Pure aapt2 + javac + d8 + apksigner pipeline (no Gradle, no AndroidX).

param(
    [switch]$Release,
    [string]$VersionName = "2.1.0",
    [int]$VersionCode = 4,
    [string]$ConfigJava = ""
)

$ErrorActionPreference = "Continue"

$Root      = Split-Path -Parent $MyInvocation.MyCommand.Path
$App       = Join-Path $Root "app"
$Src       = Join-Path $App "src"
$Res       = Join-Path $App "res"
$Manifest  = Join-Path $App "AndroidManifest.xml"
$Build     = Join-Path $Root "build"
$Dist      = Join-Path $Root "dist"

$Jdk       = "C:\AndroidDev\jdk\jdk-17.0.2"
$BuildTools = "C:\AndroidDev\tmp-bt\android-14"
$AndroidJar = "C:\AndroidDev\tmp-plat\android-34\android.jar"

$Java      = Join-Path $Jdk "bin\java.exe"
$Javac     = Join-Path $Jdk "bin\javac.exe"
$Keytool   = Join-Path $Jdk "bin\keytool.exe"
$Aapt2     = Join-Path $BuildTools "aapt2.exe"
$D8        = Join-Path $BuildTools "d8.bat"
$ZipAlign  = Join-Path $BuildTools "zipalign.exe"
$ApkSigner = Join-Path $BuildTools "apksigner.bat"

$ApkName     = "PCLProMobile-$VersionName.apk"

if ($ConfigJava -and (Test-Path $ConfigJava)) {
    Copy-Item $ConfigJava (Join-Path $Src "com\pclpro\mobile\OfficialServers.java") -Force
    Write-Host "==> OfficialServers.java <= $ConfigJava"
}

# d8.bat / apksigner.bat call bare `java`, so the JDK must be first on PATH.
$env:JAVA_HOME = $Jdk
$env:Path = (Join-Path $Jdk "bin") + ";" + $env:Path

foreach ($tool in @($Java, $Javac, $Keytool, $Aapt2, $D8, $ZipAlign, $ApkSigner, $AndroidJar)) {
    if (-not (Test-Path $tool)) { throw "Missing build tool: $tool" }
}

# Version comes from pcl-one.json (build.ps1 passes it); keep the manifest in sync
# so `aapt2 dump badging` reports the same version as the file name.
$manifestText = [System.IO.File]::ReadAllText($Manifest, [System.Text.Encoding]::UTF8)
$manifestText = [regex]::Replace($manifestText, 'android:versionCode="\d+"', ('android:versionCode="{0}"' -f $VersionCode))
$manifestText = [regex]::Replace($manifestText, 'android:versionName="[^"]*"', ('android:versionName="{0}"' -f $VersionName))
[System.IO.File]::WriteAllText($Manifest, $manifestText, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "==> clean"
if (Test-Path $Build) { Remove-Item $Build -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Build, $Dist | Out-Null
$genDir     = Join-Path $Build "gen"
$classDir   = Join-Path $Build "classes"
$dexDir     = Join-Path $Build "dex"
New-Item -ItemType Directory -Force -Path $genDir, $classDir, $dexDir | Out-Null

Write-Host "==> aapt2 compile"
$compiled = Join-Path $Build "compiled.zip"
& $Aapt2 compile --dir $Res -o $compiled
if ($LASTEXITCODE -ne 0) { throw "aapt2 compile failed" }

Write-Host "==> aapt2 link"
$unsigned = Join-Path $Build "unsigned.apk"
$Assets = Join-Path $App "assets"
$linkArgs = @("-o", $unsigned, "-I", $AndroidJar, "--manifest", $Manifest, "-R", $compiled,
    "--java", $genDir, "--min-sdk-version", "24", "--target-sdk-version", "34",
    "--version-code", [string]$VersionCode, "--version-name", $VersionName, "--auto-add-overlay")
if (Test-Path $Assets) {
    # offline guide (earth.html) etc. - packaged, no network needed
    $linkArgs += @("-A", $Assets)
}
& $Aapt2 link @linkArgs
if ($LASTEXITCODE -ne 0) { throw "aapt2 link failed" }

Write-Host "==> javac"
$sources = @()
$sources += Get-ChildItem -Path $Src -Recurse -Filter *.java | ForEach-Object { $_.FullName }
$sources += Get-ChildItem -Path $genDir -Recurse -Filter *.java | ForEach-Object { $_.FullName }
$javacArgs = @("-source", "8", "-target", "8", "-encoding", "UTF-8", "-nowarn",
    "-bootclasspath", $AndroidJar, "-d", $classDir) + $sources
& $Javac $javacArgs 2>$null
if ($LASTEXITCODE -ne 0) {
    & $Javac $javacArgs
    throw "javac failed"
}

Write-Host "==> d8"
$classFiles = Get-ChildItem -Path $classDir -Recurse -Filter *.class | ForEach-Object { $_.FullName }
& $D8 --release --lib $AndroidJar --min-api 24 --output $dexDir $classFiles
if ($LASTEXITCODE -ne 0) { throw "d8 failed" }

Write-Host "==> package dex"
$withDex = Join-Path $Build "with-dex.apk"
Copy-Item $unsigned $withDex -Force
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($withDex, [System.IO.Compression.ZipArchiveMode]::Update)
try {
    $dex = Join-Path $dexDir "classes.dex"
    $entry = $zip.CreateEntry("classes.dex", [System.IO.Compression.CompressionLevel]::Optimal)
    $input = [System.IO.File]::OpenRead($dex)
    $output = $entry.Open()
    $input.CopyTo($output)
    $output.Dispose()
    $input.Dispose()
} finally {
    $zip.Dispose()
}

Write-Host "==> zipalign"
$aligned = Join-Path $Build "aligned.apk"
& $ZipAlign -f -p 4 $withDex $aligned
if ($LASTEXITCODE -ne 0) { throw "zipalign failed" }

Write-Host "==> sign"
$keystore = Join-Path $Root "keystore\pclpro-release.jks"
if (-not (Test-Path $keystore)) {
    New-Item -ItemType Directory -Force -Path (Split-Path $keystore) | Out-Null
    & $Keytool -genkeypair -v -keystore $keystore -alias pclpro -keyalg RSA -keysize 2048 `
        -validity 10000 -storepass pclpro2026 -keypass pclpro2026 `
        -dname "CN=PCL Pro Mobile, OU=Link, O=PCL Pro, L=Local, S=Local, C=CN"
    if ($LASTEXITCODE -ne 0) { throw "keytool failed" }
}

$apk = Join-Path $Dist $ApkName
& $ApkSigner sign --ks $keystore --ks-pass pass:pclpro2026 --key-pass pass:pclpro2026 `
    --ks-key-alias pclpro --min-sdk-version 24 --out $apk $aligned
if ($LASTEXITCODE -ne 0) { throw "apksigner failed" }

Write-Host "==> verify"
& $ApkSigner verify --verbose --print-certs $apk
& $Aapt2 dump badging $apk | Select-Object -First 6

$size = (Get-Item $apk).Length
Write-Host ("==> OK  {0}  ({1:N0} bytes)" -f $apk, $size)

