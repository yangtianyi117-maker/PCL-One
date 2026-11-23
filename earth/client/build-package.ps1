<#
  Build the distributable client package and audit it for world files.
  Output: dist/PCL Earth 客户端包 <version>.zip
#>
[CmdletBinding()]
param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$root = Join-Path $PSScriptRoot "."
$dist = Join-Path (Split-Path -Parent $PSScriptRoot) "dist"
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$stage = Join-Path $env:TEMP ("pcl-earth-pack-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
New-Item -ItemType Directory -Force -Path $stage | Out-Null

Copy-Item (Join-Path $root "install-earth.ps1") $stage
Copy-Item (Join-Path $root "uninstall-earth.ps1") $stage
Copy-Item (Join-Path $root "New-ServersDat.ps1") $stage
Get-ChildItem $root -Filter "*.bat" | Copy-Item -Destination $stage
Copy-Item (Join-Path $root "README.txt") $stage
Copy-Item (Join-Path $root "payload") (Join-Path $stage "payload") -Recurse

$zipPath = Join-Path $dist ("PCL-Earth-client-{0}.zip" -f $Version)
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zipPath)

# audit: no world data may ever ship with the client
$bad = @()
$zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    foreach ($e in $zip.Entries) {
        $n = $e.FullName.Replace('\', '/').ToLowerInvariant()
        if ($n -match '(^|/)region/' -or $n -match 'level\.dat$' -or $n -match '\.mca$' -or $n -match '(^|/)dimensions/') {
            $bad += $e.FullName
        }
    }
} finally { $zip.Dispose() }

if ($bad.Count -gt 0) {
    Remove-Item $zipPath -Force
    throw ("package audit failed, world files found: " + ($bad -join ", "))
}

$size = (Get-Item $zipPath).Length
Write-Host ("built {0} ({1:N0} bytes)" -f $zipPath, $size)
Write-Host "audit: no world files"
