param(
    [string]$Version = '2026-11-20.9',
    [int]$BuildNumber = 9,
    [string]$Exe = 'dist\PCLPro.exe',
    [string]$OutputDir = 'dist\PCLPro-update-2026-11-20.9-signed',
    [string]$PrivateKey = 'secrets\pclpro-update-key.json',
    [string]$Instructions = 'docs\MERGE\UPDATE-INSTRUCTIONS.txt',
    [string]$PayloadUrl = 'http://124.221.231.213/pclpro/PCLPro.exe'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$exePath = Join-Path $root $Exe
$outDir = Join-Path $root $OutputDir
$keyPath = Join-Path $root $PrivateKey
$instructionsPath = Join-Path $root $Instructions

if (-not (Test-Path $exePath)) { throw "exe not found: $exePath" }
if (-not (Test-Path $keyPath)) { throw "private key not found: $keyPath" }
if (-not (Test-Path $instructionsPath)) { throw "instructions not found: $instructionsPath" }

function Get-Sha256Hex([byte[]]$Bytes) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

$key = Get-Content $keyPath -Raw | ConvertFrom-Json
if ($key.alg -ne 'ES256') { throw "unsupported key algorithm: $($key.alg)" }

$ecdsa = [System.Security.Cryptography.ECDsaCng]::new()
$parameters = [System.Security.Cryptography.ECParameters]::new()
$parameters.Curve = [System.Security.Cryptography.ECCurve+NamedCurves]::nistP256
$point = [System.Security.Cryptography.ECPoint]::new()
$point.X = [Convert]::FromBase64String($key.x)
$point.Y = [Convert]::FromBase64String($key.y)
$parameters.Q = $point
$parameters.D = [Convert]::FromBase64String($key.d)
$ecdsa.ImportParameters($parameters)

if (Test-Path $outDir) {
    $resolved = (Resolve-Path $outDir).Path
    if (-not $resolved.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "refusing to clean outside workspace: $resolved"
    }
    Remove-Item -Recurse -Force $outDir
}
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$exeBytes = [System.IO.File]::ReadAllBytes($exePath)
$manifest = [ordered]@{
    product     = 'PCL Pro'
    build       = "pclpro-$Version"
    buildNumber = $BuildNumber
    version     = $Version
    payload     = 'PCLPro.exe'
    payloadUrl  = $PayloadUrl
    size        = $exeBytes.Length
    sha256      = Get-Sha256Hex $exeBytes
    publishedAt = (Get-Date).ToString('o')
    keyId       = $key.keyId
    notes       = 'Signed launcher update. Verify with verify-update.ps1 before replacing.'
}
$jsonText = $manifest | ConvertTo-Json -Depth 5
$jsonBytes = [System.Text.UTF8Encoding]::new($false).GetBytes($jsonText)
$signature = $ecdsa.SignData($jsonBytes, [System.Security.Cryptography.HashAlgorithmName]::SHA256)

[System.IO.File]::WriteAllBytes((Join-Path $outDir 'update.json'), $jsonBytes)
[System.IO.File]::WriteAllText((Join-Path $outDir 'update.json.sig'),
    [Convert]::ToBase64String($signature) + "`n", [System.Text.UTF8Encoding]::new($false))
Copy-Item $exePath (Join-Path $outDir 'PCLPro.exe') -Force
Copy-Item $instructionsPath (Join-Path $outDir 'UPDATE-NOTES.txt') -Force
Set-Content -Path (Join-Path $outDir 'key-fingerprint.txt') -Value $key.keyId -Encoding ASCII

$fingerprintBytes = [System.Text.Encoding]::UTF8.GetBytes("ES256:$($key.x):$($key.y)")
$fingerprint = Get-Sha256Hex $fingerprintBytes
if ($fingerprint -ne $key.keyId) { throw "key fingerprint mismatch" }

$verifier = @'
$ErrorActionPreference = 'Stop'
$x = '__X__'
$y = '__Y__'
$expectedFingerprint = '__FP__'

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$jsonPath = Join-Path $dir 'update.json'
$sigPath = Join-Path $dir 'update.json.sig'

function Get-Sha256Hex([byte[]]$Bytes) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

$fingerprintBytes = [System.Text.Encoding]::UTF8.GetBytes("ES256:${x}:${y}")
if ((Get-Sha256Hex $fingerprintBytes) -ne $expectedFingerprint) {
    Write-Host 'FAIL public key fingerprint mismatch'
    exit 1
}

$jsonBytes = [System.IO.File]::ReadAllBytes($jsonPath)
$signature = [Convert]::FromBase64String((Get-Content $sigPath -Raw).Trim())

$ecdsa = [System.Security.Cryptography.ECDsaCng]::new()
$parameters = [System.Security.Cryptography.ECParameters]::new()
$parameters.Curve = [System.Security.Cryptography.ECCurve+NamedCurves]::nistP256
$point = [System.Security.Cryptography.ECPoint]::new()
$point.X = [Convert]::FromBase64String($x)
$point.Y = [Convert]::FromBase64String($y)
$parameters.Q = $point
$ecdsa.ImportParameters($parameters)

if (-not $ecdsa.VerifyData($jsonBytes, $signature, [System.Security.Cryptography.HashAlgorithmName]::SHA256)) {
    Write-Host 'FAIL signature invalid'
    exit 1
}

$manifest = Get-Content $jsonPath -Raw | ConvertFrom-Json
$payloadPath = Join-Path $dir $manifest.payload
if (-not (Test-Path $payloadPath)) {
    Write-Host ('FAIL payload missing: ' + $manifest.payload)
    exit 1
}

$payloadHash = Get-Sha256Hex ([System.IO.File]::ReadAllBytes($payloadPath))
if ($payloadHash -ne $manifest.sha256.ToLowerInvariant()) {
    Write-Host 'FAIL payload sha256 mismatch'
    exit 1
}
if ((Get-Item $payloadPath).Length -ne [int64]$manifest.size) {
    Write-Host 'FAIL payload size mismatch'
    exit 1
}

Write-Host ('PASS ' + $manifest.product + ' ' + $manifest.version)
Write-Host ('  payload sha256: ' + $payloadHash)
Write-Host ('  key fingerprint: ' + $expectedFingerprint)
exit 0
'@

$verifier = $verifier.Replace('__X__', $key.x).Replace('__Y__', $key.y).Replace('__FP__', $key.keyId)
[System.IO.File]::WriteAllText((Join-Path $outDir 'verify-update.ps1'), $verifier,
    [System.Text.UTF8Encoding]::new($true))

$zipPath = Join-Path $root ("$OutputDir.zip")
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zipPath -Force

Write-Host "signed package: $zipPath"
Write-Host ("  size: " + [Math]::Round((Get-Item $zipPath).Length / 1MB, 1) + " MB")
Write-Host ("  sha256: " + (Get-FileHash $zipPath -Algorithm SHA256).Hash)
Write-Host ("  key fingerprint: " + $key.keyId)
