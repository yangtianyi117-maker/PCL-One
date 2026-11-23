<#
  Coordinate contract test for the EarthMC 1:1000 (2026) Web Mercator map.
  World: x in [-19968, 19968], z in [-15360, 15744], center (0, 192).
  Formula must match docs/COORDS.md exactly.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$script:failed = 0

function Get-EarthPos([double]$lat, [double]$lon) {
    $x = $lon / 179.0 * 19968.0
    $mLat = [Math]::Log([Math]::Tan([Math]::PI / 4.0 + $lat * [Math]::PI / 360.0))
    $m80 = [Math]::Log([Math]::Tan([Math]::PI / 4.0 + 80.0 * [Math]::PI / 360.0))
    $z = 192.0 - ($mLat / $m80) * 15552.0
    return [pscustomobject]@{ x = [Math]::Round($x, 2); z = [Math]::Round($z, 2) }
}

function Assert-Near([string]$label, [double]$actual, [double]$expected, [double]$tol) {
    if ([Math]::Abs($actual - $expected) -gt $tol) {
        Write-Host ("FAIL {0}: got {1}, expected {2} +/- {3}" -f $label, $actual, $expected, $tol) -ForegroundColor Red
        $script:failed++
    } else {
        Write-Host ("ok   {0}: {1}" -f $label, $actual)
    }
}

# reference table (docs/COORDS.md)
$cities = @(
    @{ name = "Istanbul"; lat = 41.0082;  lon = 28.9784;  x = 3233;  z = -4826 },
    @{ name = "Beijing";  lat = 39.9042;  lon = 116.4074; x = 12986; z = -4664 },
    @{ name = "NewYork";  lat = 40.7128;  lon = -74.0060; x = -8256; z = -4782 },
    @{ name = "Cairo";    lat = 30.0444;  lon = 31.2357;  x = 3484;  z = -3320 },
    @{ name = "Rio";      lat = -22.9068; lon = -43.1729; x = -4816; z = 2815 },
    @{ name = "Sydney";   lat = -33.8688; lon = 151.2093; x = 16868; z = 4207 }
)

foreach ($c in $cities) {
    $p = Get-EarthPos $c.lat $c.lon
    Assert-Near ("{0}.x" -f $c.name) $p.x $c.x 3
    Assert-Near ("{0}.z" -f $c.name) $p.z $c.z 3
}

# edges & center
$n = Get-EarthPos 80 0
Assert-Near "north edge z" $n.z -15360 0.5
$s = Get-EarthPos -80 0
Assert-Near "south edge z" $s.z 15744 0.5
$w = Get-EarthPos 0 -179
Assert-Near "west edge x" $w.x -19968 0.5
$e = Get-EarthPos 0 179
Assert-Near "east edge x" $e.x 19968 0.5
$o = Get-EarthPos 0 0
Assert-Near "origin x" $o.x 0 0.01
Assert-Near "origin z" $o.z 192 0.01

if ($script:failed -gt 0) {
    Write-Host ("{0} assertion(s) failed" -f $script:failed) -ForegroundColor Red
    exit 1
}
Write-Host "all coordinate assertions passed"
exit 0
