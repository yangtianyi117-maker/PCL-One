<#
  PCL One build orchestrator - OFFLINE EDITION.

  Single source of truth: pcl-one.json + shared/ + src/ + vendor/.
  Produces:
    dist/PCL One                     (portable, self-contained distribution)
    dist/PCL One 离线包 <version>.zip (the whole thing, zipped)

  Everything the player needs is inside:
    Earth/.minecraft    26.3 + Fabric + assets + libraries + 8 mods (no download)
    Earth/java          bundled JDK 25
    Titan/              1.7.10 / 1.12.2 + java 8 + mods (portable)
    Server/             1.7.10 Forge server + world

  No launcher UI is shipped: the distribution contains PCL One.exe (windowless),
  six .lnk shortcuts and the game runtimes.

  Usage:
    .\build.ps1                 # shell + stage + zip + single exe
    .\build.ps1 -SkipZip        # no zip / single exe
    .\build.ps1 -Verify         # run offline selftest from dist after build
#>
[CmdletBinding()]
param(
    [switch]$SkipZip,
    [switch]$Verify,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

function Step([string]$message) { Write-Host ("==> {0}" -f $message) }
function Ensure([string]$path) { if (-not (Test-Path $path)) { throw "missing: $path" } }

function Copy-Tree([string]$src, [string]$dst, [string[]]$excludeDirs = @(), [string[]]$excludeFiles = @()) {
    Ensure $src
    New-Item -ItemType Directory -Force -Path $dst | Out-Null
    $roboArgs = @($src, $dst, "/E", "/NFL", "/NDL", "/NJH", "/NJS", "/NP", "/R:2", "/W:2")
    if ($excludeDirs.Count -gt 0) { $roboArgs += "/XD"; $roboArgs += $excludeDirs }
    if ($excludeFiles.Count -gt 0) { $roboArgs += "/XF"; $roboArgs += $excludeFiles }
    robocopy @roboArgs | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE): $src -> $dst" }
    $global:LASTEXITCODE = 0
}

function Write-Utf8([string]$path, [string]$text, [bool]$bom) {
    [System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($bom)))
}

function New-Lnk([string]$path, [string]$target, [string]$arguments, [string]$workDir, [string]$description) {
    $wsh = New-Object -ComObject WScript.Shell
    $sc = $wsh.CreateShortcut($path)
    $sc.TargetPath = $target
    $sc.Arguments = $arguments
    $sc.WorkingDirectory = $workDir
    $sc.Description = $description
    $sc.IconLocation = "$target,0"
    $sc.Save()
}

Step "load pcl-one.json"
Ensure (Join-Path $Root "pcl-one.json")
$cfg = Get-Content (Join-Path $Root "pcl-one.json") -Raw -Encoding UTF8 | ConvertFrom-Json
$ver = [string]$cfg.version

Step "offline payload preflight"
Ensure (Join-Path $Root "vendor\earth\game\versions")
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $Root "tools\prepare-earth-offline.ps1") -Validate
if ($LASTEXITCODE -ne 0) {
    throw "Earth offline payload invalid. Run: powershell -File tools\prepare-earth-offline.ps1"
}

Step "generate config artifacts"
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $Root "tools\gen-config.ps1")
if ($LASTEXITCODE -ne 0) { throw "gen-config failed" }
Ensure (Join-Path $Root "src\generated\OfficialServers.java")

Step "build in-game menu mod"
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $Root "tools\build-menu-mod.ps1")
if ($LASTEXITCODE -ne 0) { throw "menu mod build failed" }

Step "compile shell"
Ensure $Csc
$shellOut = Join-Path $Root "PCL One.exe"
$shellSrc = Join-Path $Root "src\shell\PCLOne.cs"
$engineSrc = Join-Path $Root "src\shell\McLaunch.cs"
Ensure $engineSrc
& $Csc /nologo /target:winexe /platform:anycpu /codepage:65001 /utf8output `
    /r:System.dll /r:System.Web.Extensions.dll `
    /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll `
    "/out:$shellOut" $shellSrc $engineSrc
if ($LASTEXITCODE -ne 0) { throw "shell compile failed" }

Step "stage dist"
$stage = Join-Path $Root "dist\PCL One"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

# --- Earth: game files + bundled Java 25 (no third-party launcher) -------------
Step "stage Earth game (offline)"
Copy-Tree (Join-Path $Root "vendor\earth\game") (Join-Path $stage "Earth\.minecraft") `
    @("logs", "crash-reports", "saves", "PCLOne-natives", ".fabric", "config", "xaero") @("PCLOne-*")
Get-ChildItem (Join-Path $Root "vendor\earth\mods") -Filter "pclone-menu-*.jar" | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $stage ("Earth\.minecraft\versions\" + $cfg.earth.instance + "\mods")) -Force
}

Step "stage Earth java 25"
Copy-Tree (Join-Path $Root "vendor\pcl-pro\PCLProRuntime\PCL\runtime\jdk-25.0.5+8") `
    (Join-Path $stage "Earth\java\jdk-25.0.5+8")

# --- Titan / Server ------------------------------------------------------------
Step "stage Titan (runtime only, no launcher)"
Copy-Tree (Join-Path $Root "vendor\titan") (Join-Path $stage "Titan") `
    @("PCL", "tools", "logs", "crash-reports", "PCLOne-natives", ".fabric", "xaero") @("Plain Craft Launcher 2.exe", "泰坦1.7.10客户端补丁.zip", "README-PCL-ONE.txt", "easytier-windows-x86_64-v2.6.4.zip", "launcher_profiles.json", "开服.bat", "查看联机地址.bat", "修复IPv6地址.bat", "PCLOne-*")
Copy-Tree (Join-Path $Root "vendor\server") (Join-Path $stage "Server")

# --- top level ----------------------------------------------------------------
Copy-Item (Join-Path $Root "PCL One.exe") $stage -Force
Copy-Item (Join-Path $Root "pcl-one.json") $stage -Force
Copy-Item (Join-Path $Root "README.md") $stage -Force
Copy-Tree (Join-Path $Root "shared") (Join-Path $stage "shared") @() @("mobile-readme.txt")
New-Item -ItemType Directory -Force -Path (Join-Path $stage "docs") | Out-Null
Get-ChildItem (Join-Path $Root "docs") -Filter *.md | Copy-Item -Destination (Join-Path $stage "docs") -Force

Step "write shortcuts (no launcher UI)"
$target = Join-Path $stage "PCL One.exe"
New-Lnk (Join-Path $stage "进入地球.lnk") $target "earth" $stage "地球服 26.3：有网自动进服，没网单机"
New-Lnk (Join-Path $stage "地球单机.lnk") $target "earth-local" $stage "地球客户端，单机启动"
New-Lnk (Join-Path $stage "泰坦 1.7.10.lnk") $target "titan1710" $stage "泰坦 1.7.10 Forge"
New-Lnk (Join-Path $stage "泰坦 1.12.2.lnk") $target "titan1122" $stage "泰坦 1.12.2 Forge"
New-Lnk (Join-Path $stage "开泰坦服.lnk") $target "server" $stage "1.7.10 Forge 服务端"
New-Lnk (Join-Path $stage "离线自检.lnk") $target "selftest" $stage "生成 PCLOne-selftest.txt"

Step "write servers.txt"
$serversLines = @(
    ("{0}|{1}:{2}|Earth\.minecraft|{3}" -f $cfg.earth.serverName, $cfg.earth.host, $cfg.earth.javaPort, $cfg.earth.instance)
)
Write-Utf8 (Join-Path $stage "servers.txt") (($serversLines -join "`r`n") + "`r`n") $false

Step "write games.txt (external game roots)"
$gamesText = "# 名称|路径（可添加任意 .minecraft 目录；绝对路径或相对本包；留空只用包内实例）`r`n"
Write-Utf8 (Join-Path $stage "games.txt") $gamesText $false

Step "write 离线包说明.txt"
$readmeText = @"
PCL One 离线包 v$ver（无界面版）
========================================

全部功能都在本地，安装/启动不下载任何东西。
没有任何启动器界面：双击快捷方式，直接进 Minecraft。

一、怎么用
  - 双击「进入地球.lnk」：进游戏后，标题界面右上角有「PCL One」按钮，全部功能都在里面
  - 菜单第一项「开始游戏（版本列表）」会列出包内所有已安装版本（地球/泰坦/自建实例），点哪个启动哪个
  - 其它按钮：进入地球（在线）、服务器列表、开泰坦服、离线自检、设置（用户名/内存）、打开综合包目录、刷新
  - 也可以直接用桌面快捷方式启动对应版本
  - 双击「PCL One.exe」= 进入地球（默认动作）

二、为什么没有外部界面
  - 全部功能集成在 Minecraft 自己的菜单里：标题界面右上角「PCL One」按钮
  - 启动逻辑在 PCL One.exe 内：版本 JSON → classpath → java，直接拉起游戏
  - 快捷方式通过参数区分功能，不弹启动器窗口
  - 单文件「PCL One 离线包 $ver.exe」解压时也不弹窗：静默解压到自身目录后直接进游戏
  - 游戏内菜单由 pclone-menu Mod 提供，只使用 Fabric Screen API，不改游戏本体

三、设置（没有设置界面，改文本文件）
  - PCLOne-settings.json：
      {"username":"Steve","memoryMb":4096}
  - username 3–16 位字母/数字/下划线；memoryMb 建议 2048–8192。
  - 改完保存，下次启动生效；用户名会写进存档。

四、地球服（26.3 Fabric）
  - 客户端：Earth\.minecraft（26.3 本体 + Fabric + assets + libraries + 8 个 Mod）
  - Java：Earth\java\jdk-25.0.5+8

五、泰坦 1.7.10 / 1.12.2
  - 客户端：Titan\.minecraft（自带 Java 8、mods、存档）

六、手机端
  - 用官方 Minecraft 基岩版直接加服务器 $($cfg.earth.host):$($cfg.earth.bedrockPort)
  - 本包不附带任何手机 App，避免非官方界面

七、说明
  - 地球世界文件不随包分发（EarthMC 地图许可要求服务器私有）。
  - 地球服是公网服务器；没有网络时地球客户端按单机模式运行。
  - 包内没有 PCL / PCL Pro / PCL2 或任何启动器界面；只有 Minecraft、Java、Forge/Fabric、Mod 这些运行时不带启动器 UI。
  - 出错时看 PCLOne-error.txt / PCLOne-last.txt。

"@
Write-Utf8 (Join-Path $stage "离线包说明.txt") $readmeText $true

Step "write PACKAGE.json"
$earthMods = @()
$modsSpecPath = Join-Path $Root "shared\mods.json"
if (Test-Path $modsSpecPath) { $earthMods = @((Get-Content $modsSpecPath -Raw -Encoding UTF8 | ConvertFrom-Json).mods) }
$pkg = [ordered]@{
    name = [string]$cfg.product
    version = $ver
    edition = "offline-native"
    offline = $true
    ui = "none"
    built = (Get-Date).ToString("yyyy-MM-dd")
    entry = "PCL One.exe (earth by default; args: earth|earth-local|titan1710|titan1122|server|selftest)"
    singleFile = ("PCL One 离线包 {0}.exe" -f $ver)
    shortcuts = @("进入地球.lnk", "地球单机.lnk", "泰坦 1.7.10.lnk", "泰坦 1.12.2.lnk", "开泰坦服.lnk", "离线自检.lnk")
    settings = "PCLOne-settings.json"
    components = [ordered]@{
        engine = [ordered]@{ type = "native"; thirdPartyLauncher = $false; note = "version JSON -> classpath -> java, built by PCL One.exe" }
        earth = [ordered]@{
            launcher = "快捷方式 进入地球.lnk -> PCL One.exe earth"
            gameDir = "Earth/.minecraft"
            instance = [string]$cfg.earth.instance
            server = ("{0}:{1}" -f $cfg.earth.host, $cfg.earth.javaPort)
            bedrock = ("{0}:{1}" -f $cfg.earth.host, $cfg.earth.bedrockPort)
            mods = @($earthMods | ForEach-Object { [string]$_.file })
            menuMod = "pclone-menu-1.0.0.jar"
            menu = "Minecraft title screen -> PCL One button"
            java = "Earth/java/jdk-25.0.5+8"
            downloads = 0
        }
        titan = [ordered]@{
            launcher = "快捷方式 泰坦 1.7.10.lnk / 泰坦 1.12.2.lnk -> PCL One.exe"
            version1710 = [string]$cfg.titan.version1710
            version1122 = [string]$cfg.titan.version1122
            java = "Titan/.minecraft/runtime/java-8"
            downloads = 0
        }
        server = [ordered]@{ path = "Server"; version = "1.7.10 Forge 10.13.4.1614"; mode = "offline" }
        phone = "official Minecraft Bedrock connects to the same server; no helper app shipped"
    }
    offlinePolicy = [ordered]@{
        gameFiles = "bundled"
        mods = "bundled"
        java = "bundled"
        thirdPartyUi = "none"
        launcherUi = "none"
        launcherUpdate = "n/a (no launcher UI)"
        startupNetworkProbe = "n/a"
        earthJoin = "online only; offline falls back to single player"
    }
}
$pkg | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $stage "PACKAGE.json") -Encoding UTF8

if ($Verify) {
    Step "verify offline selftest"
    $exe = Join-Path $stage "PCL One.exe"
    $p = Start-Process -FilePath $exe -ArgumentList "--selftest" -Wait -PassThru
    Get-Content (Join-Path $stage "PCLOne-selftest.txt") | ForEach-Object { Write-Host $_ }
    if ($p.ExitCode -ne 0) { throw "selftest failed" }
}

if (-not $SkipZip) {
    Step "zip (utf-8 entry names)"
    $zip = Join-Path $Root ("dist\PCL One 离线包 {0}.zip" -f $ver)
    if (Test-Path $zip) { Remove-Item $zip -Force }

    # tar/bsdtar on Windows writes GBK bytes for non-ASCII names, which Explorer
    # then decodes as UTF-8 -> mojibake. .NET writes UTF-8 + the 0x800 flag.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zipRoot = Join-Path $env:TEMP ("pcl-one-zip-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
    New-Item -ItemType Directory -Force -Path $zipRoot | Out-Null
    $junction = Join-Path $zipRoot "PCL One"
    New-Item -ItemType Junction -Path $junction -Target $stage | Out-Null
    $zipTmp = Join-Path $env:TEMP ("pcl-one-" + [guid]::NewGuid().ToString("N") + ".zip")
    try {
        [System.IO.Compression.ZipFile]::CreateFromDirectory(
            $zipRoot, $zipTmp, [System.IO.Compression.CompressionLevel]::Optimal, $false)
        Move-Item $zipTmp $zip -Force
    } finally {
        [System.IO.Directory]::Delete($junction, $false)
        Remove-Item $zipTmp -Force -ErrorAction SilentlyContinue
        Remove-Item $zipRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Host ("zip: {0}  ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))

    Step "build single-file offline exe (sfx)"
    $sfxSrc = Join-Path $Root "src\shell\PCLOneSetup.cs"
    $sfxInfo = Join-Path $Root "src\generated\SfxInfo.cs"
    Ensure $sfxSrc
    Ensure $sfxInfo
    $sfxBoot = Join-Path $env:TEMP ("PCLOneSetup-" + [guid]::NewGuid().ToString("N") + ".exe")
    & $Csc /nologo /target:winexe /platform:anycpu /codepage:65001 /utf8output `
        /r:System.dll `
        /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll `
        "/out:$sfxBoot" $sfxSrc $sfxInfo
    if ($LASTEXITCODE -ne 0) { throw "sfx bootstrap compile failed" }

    $sfx = Join-Path $Root ("dist\PCL One 离线包 {0}.exe" -f $ver)
    if (Test-Path $sfx) { Remove-Item $sfx -Force }
    Copy-Item $sfxBoot $sfx -Force
    $offset = (Get-Item $sfxBoot).Length
    $zipLength = (Get-Item $zip).Length
    $out = [System.IO.File]::Open($sfx, [System.IO.FileMode]::Append, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        $in = [System.IO.File]::OpenRead($zip)
        try { $in.CopyTo($out) } finally { $in.Dispose() }
        $footer = New-Object byte[] 24
        [System.Text.Encoding]::ASCII.GetBytes("PCL1SFX1").CopyTo($footer, 0)
        [BitConverter]::GetBytes([int64]$offset).CopyTo($footer, 8)
        [BitConverter]::GetBytes([int64]$zipLength).CopyTo($footer, 16)
        $out.Write($footer, 0, 24)
    } finally { $out.Dispose() }
    Remove-Item $sfxBoot -Force

    Step "verify single-file exe"
    $sfxLog = Join-Path (Split-Path $sfx) "PCLOne-sfx.log"
    Remove-Item $sfxLog -Force -ErrorAction SilentlyContinue
    $verifyProc = Start-Process -FilePath $sfx -ArgumentList "--verify" -Wait -PassThru -WorkingDirectory (Split-Path $sfx)
    if ($verifyProc.ExitCode -ne 0) {
        if (Test-Path $sfxLog) { Get-Content $sfxLog | Write-Host }
        throw "single-file exe verify failed"
    }
    if (Test-Path $sfxLog) { Get-Content $sfxLog | ForEach-Object { Write-Host $_ } }
    Remove-Item $sfxLog -Force -ErrorAction SilentlyContinue
    Write-Host ("sfx: {0}  ({1:N1} MB)" -f $sfx, ((Get-Item $sfx).Length / 1MB))
}

$sum = (Get-ChildItem $stage -Recurse -File | Measure-Object Length -Sum)
Write-Host ("dist: {0}  ({1:N1} MB / {2} files)" -f $stage, ($sum.Sum / 1MB), $sum.Count)
Write-Host "BUILD OK (offline)"
