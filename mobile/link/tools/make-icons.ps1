# Generates legacy launcher PNGs (and a 512px preview) without external tools.
Add-Type -AssemblyName System.Drawing

$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Res  = Join-Path $Root "app\res"
$blue = [System.Drawing.Color]::FromArgb(255, 14, 99, 200)

function New-Icon {
    param([int]$Size, [bool]$Round, [string]$Path)

    $bmp = New-Object System.Drawing.Bitmap -ArgumentList $Size, $Size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $brush = New-Object System.Drawing.SolidBrush -ArgumentList $blue
    $pad = [int][Math]::Round($Size * 0.02)
    $inner = $Size - (2 * $pad)
    $rect = New-Object System.Drawing.Rectangle -ArgumentList $pad, $pad, $inner, $inner

    if ($Round) {
        $g.FillEllipse($brush, $rect)
    } else {
        $d = [single]($Size * 0.22)
        $shape = New-Object System.Drawing.Drawing2D.GraphicsPath
        $shape.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
        $shape.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
        $shape.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
        $shape.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
        $shape.CloseFigure()
        $g.FillPath($brush, $shape)
        $shape.Dispose()
    }

    $s = [single]($Size / 108.0)
    $cx = [single]($Size / 2.0)
    $cy = [single]($Size / 2.0)

    function Convert-Point([single]$x, [single]$y) {
        $px = $cx + (($x - 54) * $s)
        $py = $cy + (($y - 54) * $s)
        return New-Object System.Drawing.PointF -ArgumentList ([single]$px), ([single]$py)
    }

    $top   = @((Convert-Point 54 32), (Convert-Point 78 46), (Convert-Point 54 60), (Convert-Point 30 46))
    $left  = @((Convert-Point 30 46), (Convert-Point 54 60), (Convert-Point 54 88), (Convert-Point 30 74))
    $right = @((Convert-Point 78 46), (Convert-Point 54 60), (Convert-Point 54 88), (Convert-Point 78 74))

    $white1 = New-Object System.Drawing.SolidBrush -ArgumentList ([System.Drawing.Color]::FromArgb(250, 255, 255, 255))
    $white2 = New-Object System.Drawing.SolidBrush -ArgumentList ([System.Drawing.Color]::FromArgb(184, 255, 255, 255))
    $white3 = New-Object System.Drawing.SolidBrush -ArgumentList ([System.Drawing.Color]::FromArgb(140, 255, 255, 255))

    $g.FillPolygon($white1, $top)
    $g.FillPolygon($white2, $left)
    $g.FillPolygon($white3, $right)

    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $white1.Dispose(); $white2.Dispose(); $white3.Dispose()
    $brush.Dispose(); $g.Dispose(); $bmp.Dispose()
}

$densities = [ordered]@{
    "mdpi"    = 48
    "hdpi"    = 72
    "xhdpi"   = 96
    "xxhdpi"  = 144
    "xxxhdpi" = 192
}

foreach ($d in $densities.Keys) {
    $dir = Join-Path $Res "mipmap-$d"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $size = $densities[$d]
    New-Icon -Size $size -Round $false -Path (Join-Path $dir "ic_launcher.png")
    New-Icon -Size $size -Round $true  -Path (Join-Path $dir "ic_launcher_round.png")
    Write-Host "mipmap-$d : ${size}px"
}

$preview = Join-Path $Root "dist\icon-512.png"
New-Item -ItemType Directory -Force -Path (Split-Path $preview) | Out-Null
New-Icon -Size 512 -Round $false -Path $preview
Write-Host "preview: $preview"
