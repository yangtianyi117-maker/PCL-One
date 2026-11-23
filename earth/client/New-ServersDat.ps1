<#
.SYNOPSIS
  Read / merge / write Minecraft servers.dat (gzip + NBT) with no third-party deps.

.DESCRIPTION
  servers.dat layout:
    TAG_Compound("") { TAG_List("servers", TAG_Compound) [ {name, ip, icon, ...}, ... ] }

  Only name/ip/icon are preserved; unknown tags are skipped by type.
  Writes acceptTextures=1 so 1.20+ clients render the entry normally.

  Usage:
    .\New-ServersDat.ps1 -Path "$env:APPDATA\.minecraft\servers.dat" `
        -Name "Earth" -Address "124.221.231.213:25566"

  Behavior:
    - existing and parseable: keep entries (dedupe by ip), upsert the given one
    - missing/corrupt: back up, write a new file with only this entry
    - always backs up to servers.dat.bak-earth-<timestamp> before writing

  NOTE: ASCII-only on purpose. Windows PowerShell 5.1 reads BOM-less .ps1 as ANSI,
  which corrupts non-ASCII string literals.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [Parameter(Mandatory = $true)][string]$Name,
    [Parameter(Mandatory = $true)][string]$Address,
    [string]$IconBase64 = "",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# --------------------------------------------------------------------------- #
# big-endian helpers
# --------------------------------------------------------------------------- #

function Read-BEU16([System.IO.BinaryReader]$br) {
    $b = $br.ReadBytes(2)
    [Array]::Reverse($b)
    return [BitConverter]::ToUInt16($b, 0)
}

function Read-BEI32([System.IO.BinaryReader]$br) {
    $b = $br.ReadBytes(4)
    [Array]::Reverse($b)
    return [BitConverter]::ToInt32($b, 0)
}

function Read-NbtString([System.IO.BinaryReader]$br) {
    $len = Read-BEU16 $br
    $bytes = $br.ReadBytes([int]$len)
    return [System.Text.Encoding]::UTF8.GetString($bytes)
}

function Skip-NbtPayload([System.IO.BinaryReader]$br, [byte]$type) {
    switch ($type) {
        1 { [void]$br.ReadByte() }
        2 { [void]$br.ReadBytes(2) }
        3 { [void]$br.ReadBytes(4) }
        4 { [void]$br.ReadBytes(8) }
        5 { [void]$br.ReadBytes(4) }
        6 { [void]$br.ReadBytes(8) }
        7 { $n = Read-BEI32 $br; [void]$br.ReadBytes($n) }
        8 { $n = Read-BEU16 $br; [void]$br.ReadBytes([int]$n) }
        9 {
            $elem = $br.ReadByte()
            $n = Read-BEI32 $br
            for ($i = 0; $i -lt $n; $i++) { Skip-NbtPayload $br $elem }
        }
        10 {
            while ($true) {
                $t = $br.ReadByte()
                if ($t -eq 0) { break }
                [void](Read-NbtString $br)
                Skip-NbtPayload $br $t
            }
        }
        11 { $n = Read-BEI32 $br; [void]$br.ReadBytes($n * 4) }
        12 { $n = Read-BEI32 $br; [void]$br.ReadBytes($n * 8) }
        default { throw "Unknown NBT tag type: $type" }
    }
}

function Read-NbtServers([string]$file) {
    $raw = [System.IO.File]::ReadAllBytes($file)
    $ms = New-Object System.IO.MemoryStream(,$raw)
    # Vanilla writes gzip; some launchers write raw NBT. Accept both.
    if ($raw.Length -ge 2 -and $raw[0] -eq 0x1F -and $raw[1] -eq 0x8B) {
        $stream = New-Object System.IO.Compression.GZipStream($ms, [System.IO.Compression.CompressionMode]::Decompress)
    } else {
        $stream = $ms
    }
    $br = New-Object System.IO.BinaryReader($stream)

    $rootType = $br.ReadByte()
    if ($rootType -ne 10) { throw "Root tag is not Compound" }
    [void](Read-NbtString $br)

    $result = New-Object System.Collections.ArrayList
    while ($true) {
        $type = $br.ReadByte()
        if ($type -eq 0) { break }
        $name = Read-NbtString $br
        if ($type -eq 9 -and $name -eq "servers") {
            $elem = $br.ReadByte()
            $count = Read-BEI32 $br
            if ($elem -ne 10) { throw "servers list element is not Compound" }
            for ($i = 0; $i -lt $count; $i++) {
                $entry = @{ name = ""; ip = ""; icon = "" }
                while ($true) {
                    $t = $br.ReadByte()
                    if ($t -eq 0) { break }
                    $key = Read-NbtString $br
                    if ($t -eq 8) {
                        $value = Read-NbtString $br
                        if ($key -eq "name" -or $key -eq "ip" -or $key -eq "icon") { $entry[$key] = $value }
                    } else {
                        Skip-NbtPayload $br $t
                    }
                }
                [void]$result.Add([pscustomobject]$entry)
            }
        } else {
            Skip-NbtPayload $br $type
        }
    }
    $br.Dispose()
    if ($stream -ne $ms) { $stream.Dispose() }
    $ms.Dispose()
    return $result
}

function Write-BEU16([System.IO.MemoryStream]$ms, [int]$value) {
    $b = [BitConverter]::GetBytes([uint16]$value)
    [Array]::Reverse($b)
    $ms.Write($b, 0, 2)
}

function Write-BEI32([System.IO.MemoryStream]$ms, [int]$value) {
    $b = [BitConverter]::GetBytes([int32]$value)
    [Array]::Reverse($b)
    $ms.Write($b, 0, 4)
}

function Write-NbtString([System.IO.MemoryStream]$ms, [string]$value) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($value)
    Write-BEU16 $ms $bytes.Length
    $ms.Write($bytes, 0, $bytes.Length)
}

function Write-NbtServers([string]$file, $servers) {
    $ms = New-Object System.IO.MemoryStream
    $ms.WriteByte(10)
    Write-NbtString $ms ""
    $ms.WriteByte(9)
    Write-NbtString $ms "servers"
    $ms.WriteByte(10)
    Write-BEI32 $ms $servers.Count
    foreach ($s in $servers) {
        $ms.WriteByte(8); Write-NbtString $ms "name"; Write-NbtString $ms ([string]$s.name)
        $ms.WriteByte(8); Write-NbtString $ms "ip"; Write-NbtString $ms ([string]$s.ip)
        $ms.WriteByte(8); Write-NbtString $ms "icon"; Write-NbtString $ms ([string]$s.icon)
        $ms.WriteByte(1); Write-NbtString $ms "acceptTextures"; $ms.WriteByte(1)
        $ms.WriteByte(0)
    }
    $ms.WriteByte(0)
    $nbt = $ms.ToArray()
    $ms.Dispose()

    $out = New-Object System.IO.MemoryStream
    $gz = New-Object System.IO.Compression.GZipStream($out, [System.IO.Compression.CompressionMode]::Compress)
    $gz.Write($nbt, 0, $nbt.Length)
    $gz.Dispose()
    $bytes = $out.ToArray()
    $out.Dispose()

    $dir = Split-Path -Parent $file
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    [System.IO.File]::WriteAllBytes($file, $bytes)
}

# --------------------------------------------------------------------------- #
# main
# --------------------------------------------------------------------------- #

$existing = @()
if (Test-Path $Path) {
    try {
        $existing = @(Read-NbtServers $Path)
        Write-Host ("[servers.dat] existing entries: {0}" -f $existing.Count)
    } catch {
        Write-Warning ("[servers.dat] parse failed, will write a new file: {0}" -f $_.Exception.Message)
        if (-not $DryRun) {
            $bak = "$Path.bak-earth-" + (Get-Date -Format "yyyyMMdd-HHmmss-fff")
            Copy-Item $Path $bak -Force
            Write-Host "[servers.dat] corrupt file backed up to $bak"
        }
        $existing = @()
    }
}

$merged = New-Object System.Collections.ArrayList
foreach ($s in $existing) { [void]$merged.Add($s) }

$hit = $merged | Where-Object { $_.ip -eq $Address } | Select-Object -First 1
if ($hit) {
    $hit.name = $Name
} else {
    [void]$merged.Add([pscustomobject]@{ name = $Name; ip = $Address; icon = $IconBase64 })
}

Write-Host ("[servers.dat] target: {0} -> {1}" -f $Name, $Address)
foreach ($s in $merged) { Write-Host ("  - {0}  {1}" -f $s.name, $s.ip) }

if ($DryRun) {
    Write-Host "[servers.dat] DryRun, nothing written"
    return
}

if (Test-Path $Path) {
    $bak = "$Path.bak-earth-" + (Get-Date -Format "yyyyMMdd-HHmmss-fff")
    Copy-Item $Path $bak -Force
    Write-Host "[servers.dat] backed up to $bak"
}

Write-NbtServers -file $Path -servers $merged

$check = @(Read-NbtServers $Path)
if (-not ($check | Where-Object { $_.ip -eq $Address })) {
    throw "[servers.dat] read-back verification failed"
}
Write-Host ("[servers.dat] wrote {0} entries" -f $check.Count)
