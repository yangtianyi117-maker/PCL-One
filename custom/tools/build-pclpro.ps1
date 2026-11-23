param(
    [string]$Configuration = 'Release',
    [string]$Output = 'dist\PCLPro.exe',
    [string]$EasyTierSource = 'tools\easytier\pcl-src',
    [string]$PayloadVersion = 'pclpro-2026-11-21.17',
    [string]$DotNetPath = '',
    [string]$ZigPath = '',
    [switch]$PublicRelease,
    [switch]$DryRun,
    [switch]$AllowNoMsClientId
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$pcl = Join-Path $root 'tools\pclce\PCL-CE-dev'
$build = Join-Path $root 'build\pclpro'
$publish = Join-Path $build 'pcl'
$bootstrapper = Join-Path $root 'tools\bootstrapper'
$outputPath = Join-Path $root $Output
$easyTierSource = Join-Path $root $EasyTierSource

# 解析外部工具：优先显式参数，其次仓库内本地副本，最后 PATH。
# 这样仓库本身可以保持轻量（SDK/Zig 不入库），别人克隆后装好 dotnet/zig 即可构建。
function Resolve-Tool {
    param(
        [string]$Explicit,
        [string]$LocalPath,
        [string]$CommandName,
        [string]$Hint
    )
    if ($Explicit) {
        if (-not (Test-Path $Explicit)) { throw "指定的 $CommandName 不存在: $Explicit" }
        return (Resolve-Path $Explicit).Path
    }
    if (Test-Path $LocalPath) { return (Resolve-Path $LocalPath).Path }
    $cmd = Get-Command $CommandName -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw "找不到 $CommandName。$Hint"
}

$dotnet = Resolve-Tool -Explicit $DotNetPath -LocalPath (Join-Path $root 'tools\dotnet-sdk\dotnet.exe') `
    -CommandName 'dotnet' -Hint '请安装 .NET 10 SDK 并确保 dotnet 在 PATH 中，或放到 tools\dotnet-sdk\dotnet.exe。'
$zig = Resolve-Tool -Explicit $ZigPath -LocalPath (Join-Path $root 'tools\zig-windows-x86_64-0.13.0\zig.exe') `
    -CommandName 'zig' -Hint '请安装 Zig 0.13 并确保 zig 在 PATH 中，或放到 tools\zig-windows-x86_64-0.13.0\zig.exe。'

if (-not (Test-Path $pcl)) { throw "PCL source not found: $pcl" }

$hasEasyTier = Test-Path $easyTierSource

if ($DryRun) {
    Write-Host "dotnet : $dotnet"
    Write-Host "zig    : $zig"
    Write-Host "source : $pcl"
    Write-Host "output : $outputPath"
    Write-Host "easytier: $(if ($hasEasyTier) { $easyTierSource } else { '未找到，将跳过（EasyTier 由启动器按需下载）' })"
    exit 0
}

if (-not $hasEasyTier) {
    Write-Warning "未找到 EasyTier 源码目录 $easyTierSource，打包时跳过。需要局域网联机时可让启动器自行下载。"
}

# 微软正版登录需要构建时注入 OAuth Client ID（上游 CI 用 secret CLIENT_ID → PCL_MS_CLIENT_ID）。
# 没有注入时 MsLogin 会直接报 "Microsoft OAuth client id is not configured"。
if ($PublicRelease -and -not $AllowNoMsClientId -and [string]::IsNullOrWhiteSpace($env:PCL_MS_CLIENT_ID)) {
    throw @"
公开构建缺少环境变量 PCL_MS_CLIENT_ID：构建出的启动器无法使用微软正版登录。
  1) 在 Microsoft Entra 注册一个公共客户端应用（支持个人 Microsoft 账户、允许 public client flows）；
  2) 设置 `$env:PCL_MS_CLIENT_ID = '<Application (client) ID>'` 后重新构建；
  3) 确实不需要正版登录时，加 -AllowNoMsClientId 跳过此检查。
"@
}
if (-not [string]::IsNullOrWhiteSpace($env:PCL_MS_CLIENT_ID)) {
    # 让 Source Generator 把 PCL_* 环境变量写进程序集；上游 CI 也是这么做的。
    $env:PCL_WRITE_SECRET = '1'
    $leaky = Get-ChildItem Env: | Where-Object { $_.Name -like 'PCL_*' -and $_.Name -notin @('PCL_MS_CLIENT_ID','PCL_WRITE_SECRET') }
    if ($leaky) {
        Write-Warning ("以下 PCL_* 环境变量也会被写进二进制，确认它们该被签名分发: " + (($leaky.Name) -join ', '))
    }
}
# Isolate build environment from non-writable user folders.
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $root '.nuget'
$env:TMP = Join-Path $root '.tmp'
$env:TEMP = Join-Path $root '.tmp'
$env:ZIG_GLOBAL_CACHE_DIR = Join-Path $root '.zig-global'
$env:ZIG_LOCAL_CACHE_DIR = Join-Path $root '.zig-cache'
New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME, $env:NUGET_PACKAGES, $env:TMP, $env:ZIG_GLOBAL_CACHE_DIR, $env:ZIG_LOCAL_CACHE_DIR | Out-Null

Write-Host "[1/5] Clean build directory: $build"
if (Test-Path $build) {
    $resolved = (Resolve-Path $build).Path
    if (-not $resolved.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean outside workspace: $resolved"
    }
    Remove-Item -Recurse -Force $build
}
New-Item -ItemType Directory -Force -Path $publish | Out-Null

Write-Host "[2/5] dotnet publish"
$publishArgs = @(
    (Join-Path $pcl 'Plain Craft Launcher 2\Plain Craft Launcher 2.csproj'),
    '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'true',
    '-p:PublishSingleFile=false', '-p:LangVersion=14', '-p:Platform=x64',
    '-o', $publish
)
if ($PublicRelease) {
    # 公开发布必须带此项：启用正版购买劝导（《PCL 存储库合理使用指南》重度使用第 4 条）
    Write-Host "      public release: yes (PUBLIC_RELEASE)"
    $publishArgs += '-p:PublicRelease=true'
}
& $dotnet publish @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }

Write-Host "[3/5] Assemble payload (runtime + EasyTier)"
if ($hasEasyTier) {
    $easyTierTarget = Join-Path $publish 'PCL\Local\EasyTier'
    New-Item -ItemType Directory -Force -Path $easyTierTarget | Out-Null
    Copy-Item -Recurse -Force (Join-Path $easyTierSource '*') $easyTierTarget
}

$payloadZip = Join-Path $build 'payload.zip'
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $payloadZip -Force
Copy-Item -Force $payloadZip (Join-Path $bootstrapper 'payload.zip')

Write-Host "[4/5] Build bootstrapper"
New-Item -ItemType Directory -Force -Path (Split-Path $outputPath -Parent) | Out-Null
Push-Location $bootstrapper
try {
    & $zig build-exe main.zig -O ReleaseSmall
    if ($LASTEXITCODE -ne 0) { throw "zig build failed: $LASTEXITCODE" }
    Copy-Item -Force (Join-Path $bootstrapper 'main.exe') $outputPath
    Remove-Item -Force (Join-Path $bootstrapper 'main.exe.obj') -ErrorAction SilentlyContinue
}
finally {
    Pop-Location
}

Write-Host "[5/5] Output"
$file = Get-Item $outputPath
$hash = (Get-FileHash $outputPath -Algorithm SHA256).Hash
Write-Host ("  " + $file.FullName)
Write-Host ("  " + [Math]::Round($file.Length / 1MB, 1) + " MB  SHA256 " + $hash)
Write-Host ("  payload version constant: " + $PayloadVersion + " (keep tools\bootstrapper\main.zig in sync)")
