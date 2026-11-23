param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][int]$BuildNumber,
    [string]$PayloadUrl = 'http://124.221.231.213/pclpro/PCLPro.exe',
    [switch]$SkipSign,
    [switch]$SkipUpload
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $root 'tools\dotnet-sdk\dotnet.exe'
$sshdeploy = Join-Path $root 'tools\deploy\bin\Release\net10.0\sshdeploy.dll'
$signScript = Join-Path $root 'tools\sign-pclpro-update.ps1'
$exe = Join-Path $root 'dist\PCLPro.exe'
$package = Join-Path $root ("dist\PCLPro-update-$Version-signed")

if (-not (Test-Path $exe)) { throw "missing exe: $exe (run tools\build-pclpro.ps1 first)" }
if (-not (Test-Path $sshdeploy)) { throw "missing sshdeploy: $sshdeploy (build tools\deploy first)" }
if (-not $env:PCL_VPS_PASSWORD) { throw "set PCL_VPS_PASSWORD for the VPS upload" }

if (-not $SkipSign) {
    Write-Host "[1/3] sign $Version (build $BuildNumber)"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $signScript `
        -Version $Version -BuildNumber $BuildNumber `
        -OutputDir ("dist\PCLPro-update-$Version-signed") `
        -PayloadUrl $PayloadUrl
    if ($LASTEXITCODE -ne 0) { throw "sign failed: $LASTEXITCODE" }
}

if (-not $SkipUpload) {
    function Send-UpdateFile([string]$Local, [string]$Remote) {
        & $dotnet $sshdeploy upload $Local $Remote
        if ($LASTEXITCODE -ne 0) { throw "upload failed: $Local" }
    }

    Write-Host "[2/3] upload exe first (112 MB, may take a few minutes)"
    Send-UpdateFile (Join-Path $root 'dist\PCLPro.exe') '/var/www/mc/pclpro/PCLPro.exe'

    Write-Host "[2/3] verify served exe size before publishing manifest"
    $head = Invoke-WebRequest 'http://124.221.231.213/pclpro/PCLPro.exe' -Method Head -UseBasicParsing -TimeoutSec 15
    $expectedSize = (Get-Item $exe).Length
    $servedSize = [int64]$head.Headers['Content-Length']
    if ($servedSize -ne $expectedSize) {
        throw "served exe size $servedSize != local $expectedSize; manifest NOT updated"
    }

    Write-Host "[3/3] upload manifest after exe"
    Send-UpdateFile (Join-Path $package 'update.json') '/var/www/mc/pclpro/update.json'
    Send-UpdateFile (Join-Path $package 'update.json.sig') '/var/www/mc/pclpro/update.json.sig'
}

Write-Host "[check] serving manifest"
$manifest = Invoke-WebRequest 'http://124.221.231.213/pclpro/update.json' -UseBasicParsing -TimeoutSec 15
$manifestJson = $manifest.Content | ConvertFrom-Json
if ($manifestJson.buildNumber -ne $BuildNumber) {
    throw "served manifest buildNumber $($manifestJson.buildNumber) != $BuildNumber"
}
Write-Host $manifest.Content
Write-Host "[check] exe headers"
$head = Invoke-WebRequest 'http://124.221.231.213/pclpro/PCLPro.exe' -Method Head -UseBasicParsing -TimeoutSec 15
Write-Host ("  status=" + $head.StatusCode + " size=" + $head.Headers['Content-Length'])
if ([int64]$head.Headers['Content-Length'] -ne (Get-Item $exe).Length) {
    throw "served exe size does not match local exe"
}
