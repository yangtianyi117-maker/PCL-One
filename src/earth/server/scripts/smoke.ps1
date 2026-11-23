<#
  Smoke test for a running PCL Earth server:
    - Java Server List Ping (25565)
    - Bedrock RakNet unconnected ping (19132/udp)
    - control-plane servers.json (HTTP)
#>
[CmdletBinding()]
param(
    [string]$HostName = "124.221.231.213",
    [int]$JavaPort = 25566,
    [int]$BedrockPort = 19132,
    [string]$ControlUrl = ""
)

$ErrorActionPreference = "Continue"
$failed = 0

function Ok([string]$label, [string]$detail = "") { Write-Host ("ok   {0} {1}" -f $label, $detail) }
function Bad([string]$label, [string]$detail = "") { Write-Host ("FAIL {0} {1}" -f $label, $detail) -ForegroundColor Red; $script:failed++ }

function Write-VarInt($stream, [int]$value) {
    while ($true) {
        $b = $value -band 0x7F
        $value = $value -shr 7
        if ($value -ne 0) { $stream.WriteByte([byte]($b -bor 0x80)) } else { $stream.WriteByte([byte]$b); break }
    }
}

function Read-VarInt($stream) {
    $num = 0; $shift = 0
    for ($i = 0; $i -lt 5; $i++) {
        $b = $stream.ReadByte()
        if ($b -lt 0) { throw "eof" }
        $num = $num -bor (($b -band 0x7F) -shl $shift)
        if (($b -band 0x80) -eq 0) { return $num }
        $shift += 7
    }
    throw "varint too long"
}

function Ping-Java([string]$h, [int]$p) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $client.Connect($h, $p)
        $stream = $client.GetStream()
        $stream.ReadTimeout = 4000
        $ms = New-Object System.IO.MemoryStream
        Write-VarInt $ms 0
        Write-VarInt $ms 47
        $hb = [System.Text.Encoding]::UTF8.GetBytes($h)
        Write-VarInt $ms $hb.Length
        $ms.Write($hb, 0, $hb.Length)
        $pb = [BitConverter]::GetBytes([uint16]$p); [Array]::Reverse($pb)
        $ms.Write($pb, 0, 2)
        Write-VarInt $ms 1
        $body = $ms.ToArray(); $ms.Dispose()
        Write-VarInt $stream $body.Length
        $stream.Write($body, 0, $body.Length)
        Write-VarInt $stream 1
        $stream.WriteByte(0)

        $len = Read-VarInt $stream
        $buf = New-Object byte[] $len
        $read = 0
        while ($read -lt $len) { $read += $stream.Read($buf, $read, $len - $read) }
        $rm = New-Object System.IO.MemoryStream(,$buf)
        [void](Read-VarInt $rm)
        $slen = Read-VarInt $rm
        $sb = New-Object byte[] $slen
        [void]$rm.Read($sb, 0, $slen)
        return [System.Text.Encoding]::UTF8.GetString($sb)
    } finally {
        $client.Close()
    }
}

function Ping-Bedrock([string]$h, [int]$p) {
    $udp = New-Object System.Net.Sockets.UdpClient
    try {
        $udp.Client.ReceiveTimeout = 2500
        $magic = [byte[]](0x00,0xFF,0xFF,0x00,0xFE,0xFE,0xFE,0xFE,0xFD,0xFD,0xFD,0xFD,0x12,0x34,0x56,0x78)
        $ts = [BitConverter]::GetBytes([long]([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())); [Array]::Reverse($ts)
        $guid = [BitConverter]::GetBytes([long]42); [Array]::Reverse($guid)
        $pkt = New-Object System.Collections.ArrayList
        [void]$pkt.Add([byte]0x01)
        foreach ($b in $ts) { [void]$pkt.Add($b) }
        foreach ($b in $magic) { [void]$pkt.Add($b) }
        foreach ($b in $guid) { [void]$pkt.Add($b) }
        $bytes = $pkt.ToArray()
        [void]$udp.Send($bytes, $bytes.Length, $h, $p)
        $ep = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
        $resp = $udp.Receive([ref]$ep)
        if ($resp.Length -lt 35 -or $resp[0] -ne 0x1C) { throw "unexpected RakNet reply" }
        $slen = ($resp[35] -shl 8) -bor $resp[36]
        return [System.Text.Encoding]::UTF8.GetString($resp, 37, [Math]::Min($slen, $resp.Length - 37))
    } finally {
        $udp.Close()
    }
}

Write-Host "== java $HostName`:$JavaPort =="
try {
    $json = Ping-Java $HostName $JavaPort
    $o = $json | ConvertFrom-Json
    Ok "java ping" ("version={0} players={1}/{2}" -f $o.version.name, $o.players.online, $o.players.max)
} catch { Bad "java ping" $_.Exception.Message }

Write-Host "== bedrock $HostName`:$BedrockPort/udp =="
try {
    $motd = Ping-Bedrock $HostName $BedrockPort
    Ok "bedrock ping" $motd.Substring(0, [Math]::Min(80, $motd.Length))
} catch { Bad "bedrock ping" $_.Exception.Message }

if (-not $ControlUrl) { $ControlUrl = "http://$HostName/pclpro/servers.json" }
Write-Host "== control $ControlUrl =="
try {
    $r = Invoke-WebRequest -Uri $ControlUrl -UseBasicParsing -TimeoutSec 6
    $j = $r.Content | ConvertFrom-Json
    $earth = @($j.servers | Where-Object { $_.id -eq "earth" })
    if ($earth.Count -eq 1) { Ok "servers.json earth entry" $earth[0].name } else { Bad "servers.json earth entry" "not found" }
} catch { Bad "servers.json" $_.Exception.Message }

if ($failed -gt 0) { Write-Host ("{0} smoke check(s) failed" -f $failed) -ForegroundColor Red; exit 1 }
Write-Host "smoke passed"
exit 0
