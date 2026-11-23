# PCL 定制版（PCL Custom）

[![build](https://github.com/yangtianyi117-maker/PCL-Custom/actions/workflows/build.yml/badge.svg)](https://github.com/yangtianyi117-maker/PCL-Custom/actions/workflows/build.yml)
[English](README.md)

> 第三方基于 [PCL](https://github.com/Meloong-Git/PCL) 与 [PCL CE](https://github.com/PCL-Community/PCL-CE) 的独立二次创作。
> **与 PCL 官方无关，不是官方后续版本**。作者：龙腾猫跃 · 赞助：<https://afdian.com/a/LTCat>
>
> 英文名：**PCL Custom**。开发期代号是 `PCL Pro`；按上游《PCL 存储库合理使用指南》，对外名称统一为 **PCL 定制版 / PCL Custom**（`docs/MERGE` 等内部设计文档可能仍沿用旧代号）。

## 这是什么

一个 Minecraft Java 版启动器，在上游基础上做了几处工程化改造：

| 能力 | 说明 |
|---|---|
| **服务器列表** | 像浏览模组一样挑服务器：一行一台，实时 ping 人数/延迟，点「进入」直连，点复制拿基岩版地址 |
| **手机联机** | 官方基岩服一键进、同 Wi-Fi 基岩桥（Geyser）、`U/` 房间码邀请；详见 [`docs/MULTIPLAYER.md`](docs/MULTIPLAYER.md)，手机源码 [PCL-Mobile-Link](https://github.com/yangtianyi117-maker/PCL-Mobile-Link) |
| **Mod 收件箱** | 把 jar 丢进 `PCL\ModInbox` 自动识别加载器/版本并安装，缺的 `required` 依赖自动从 Modrinth 补 |
| **签名自动更新** | ECDSA P-256 manifest + exe SHA-256 校验，Range 断点续传，只替换 bootstrapper |
| **便携模式** | 所有可写数据在 exe 同目录 `PCL\`，不碰 `%AppData%` |
| **界面整理** | 去掉了启动弹窗与冗余入口，服务器入口置顶 |

启动器内核（下载、安装、模组/整合包、皮肤、崩溃分析等）沿用上游。

## PCL One · 离线原生启动（5.0.0）

[PCL-One](https://github.com/yangtianyi117-maker/PCL-One) 是第一方离线发行版：没有第三方启动器界面，直接
`版本 JSON → classpath → java`，并在 26.3 客户端里提供游戏内 Fabric 菜单
（`pcl-one-menu`）。包内含地球 26.3、泰坦 1.7.10/1.12.2、1.7.10 Forge 服务端、
6 个快捷方式和静默单文件安装器。

- 源码：[PCL-One](https://github.com/yangtianyi117-maker/PCL-One)
- 设计：[OFFLINE.md](https://github.com/yangtianyi117-maker/PCL-One/blob/main/docs/OFFLINE.md)、[DESIGN.md](https://github.com/yangtianyi117-maker/PCL-One/blob/main/docs/DESIGN.md)
- 构建：在 PCL-One 仓库执行 `powershell -File build.ps1 -Verify`（需要 `vendor/` 二进制，不入库）
- 手机联机助手源码：PCL-One 的 `src/mobile`（同步到
  [PCL-Mobile-Link](https://github.com/yangtianyi117-maker/PCL-Mobile-Link)）

启动器本体（`PCLPro.exe`、扩展中心、签名更新）仍在本仓库；PCL One 是建立在同一套
Minecraft 运行时之上的自包含发行版。

## 扩展中心（旁路工具）

`PCLPro.exe` 是编译后的程序，没有源码，所以桌面端附带了**不碰启动器进程**的旁路工具：

- **客户端 Mod**：从 Modrinth 解析当前实例的构建（含 required 前置），下载到 `<游戏目录>/mods`；
- **世界服务器**：把 `pcl_servers.json` 合并进 `<游戏目录>/servers.dat`，写入前自动备份
  `servers.dat.bak-<时间戳>`。

只用 Python 3 标准库；Mod 安装默认 dry-run，只预览不写入。完整命令见
[`扩展中心/README.md`](扩展中心/README.md)。

## 下载

不想自己构建的话，到 [Releases](https://github.com/yangtianyi117-maker/PCL-Custom/releases/latest) 下载
`PCL-Custom-v1.1.0-win64.zip`（Windows x64，解压后双击 `PCLPro.exe`）。

- SHA-256：`F851C0EC1669D912FEEDDCBEF6F4158FD6574CBC594AADCAD588E0E3609774FC`
- 国内镜像（同一文件、同一 SHA-256）：<http://124.221.231.213/pclpro/PCL-Custom-v1.1.0-win64.zip>

### 国内下载慢怎么办

GitHub 的 Release 资产走 Fastly，挂梯子后路由经常反而更慢。国内优先用上面的镜像：
它在中国大陆的腾讯云 VPS 上，支持 HTTP Range（断点续传）：

```bash
aria2c -x 8 -s 8 -c -o PCL-Custom-v1.1.0-win64.zip \
  http://124.221.231.213/pclpro/PCL-Custom-v1.1.0-win64.zip
```

镜像上行约 5 Mbps（≈600 KB/s），117 MB 大约 3–4 分钟。若你的国际线路很快，GitHub 可能更快；
若开梯子反而更慢，换节点，或改用 GitHub 代理（如 `https://ghproxy.net/https://github.com/...`）。

下载后请务必核对上面的 SHA-256。

## 构建

前置：Windows x64、[.NET 10 SDK](https://dotnet.microsoft.com/)、[Zig 0.13](https://ziglang.org/)。

脚本按「显式参数 → 仓库内 `tools\dotnet-sdk` / `tools\zig-windows-x86_64-0.13.0` → PATH」的顺序找工具，
所以克隆后只要 `dotnet` 和 `zig` 在 PATH 里就能构建（SDK 与 Zig 不入库）。先自检：

```powershell
powershell -ExecutionPolicy Bypass -File tools\build-pclpro.ps1 -DryRun
```

再构建：

```powershell
# 开发构建（不启用正版购买劝导，仅限自用）
powershell -ExecutionPolicy Bypass -File tools\build-pclpro.ps1

# 公开发布构建（必须：许可要求）
powershell -ExecutionPolicy Bypass -File tools\build-pclpro.ps1 -PublicRelease
```

产物：`dist\PCLPro.exe`（bootstrapper）→ 首次运行解压 `PCLProRuntime\`。

> 注意：项目要求 `LangVersion=14`（.NET 10 默认值），构建脚本已处理。
>
> **微软正版登录**需要构建时注入 OAuth Client ID（`PCL_MS_CLIENT_ID`），仓库不附带该 ID。没有注入的公开构建无法登录微软账号；`-PublicRelease` 会直接拒绝构建，除非加 `-AllowNoMsClientId`。
> EasyTier 目录（`tools\easytier\pcl-src`）是可选的；缺失时脚本会跳过并警告，启动器可自行下载。

## 目录

```
tools/pclce/PCL-CE-dev/   启动器源码（上游 + 本项目修改）
tools/bootstrapper/       Zig 写的 exe 外壳，负责解压/透传参数/自更新
tools/research/           联机服务器搜集脚本（见 docs/research）
docs/                     设计与检索记录
```

## 许可

- 根 `LICENSE`：Apache-2.0（`Plain Craft Launcher 2/` 以外目录）
- `LICENCE`：《PCL 分发有限许可》+《PCL 存储库合理使用指南》（`Plain Craft Launcher 2/` 目录）
- 本项目修改同样受上述许可约束；公开发布构建必须启用正版购买劝导（`-PublicRelease`）

## 安全

私钥与服务器凭据不在仓库内，见 [SECURITY.md](SECURITY.md)。

## 免责声明

本项目按"原样"提供，不提供任何担保；请遵守 Minecraft 最终用户许可协议与当地法律。
