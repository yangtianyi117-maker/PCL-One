# PCL Mobile / PCL 定制版 手机端

PCL-branded Android launcher built on PojavLauncher (GPLv3), with offline-first
downloads, quick-join, and a compliance-first multiplayer plan.

PCL 品牌 Android 启动器，基于 PojavLauncher（GPLv3），离线优先下载、快速加入服务器，
联机与互通方案以合规为第一原则。

- Upstream / 上游: https://github.com/PojavLauncherTeam/PojavLauncher
- License / 许可: **GPLv3** (see `LICENSE`)
- Audit / 审计: [`docs/FIRST-PRINCIPLES-AUDIT.md`](docs/FIRST-PRINCIPLES-AUDIT.md)

## 与 PCL One 的关系 / How it relates to PCL One

PCL One 离线包（[PCL-One](https://github.com/yangtianyi117-maker/PCL-One)，
5.0.0）**不附带手机 App**：手机用官方 Minecraft 基岩版直连 `124.221.231.213:19132`。
需要「一键进入 / 局域网发现 / 房间码 / 离线地球指南」时安装
[PCL-Mobile-Link](https://github.com/yangtianyi117-maker/PCL-Mobile-Link)（2.1.0）。
本仓库（PojavLauncher fork）继续作为 Android 上的 Java 启动器独立发布，不进入离线包。

详见 [`docs/PCL-ONE.md`](docs/PCL-ONE.md)。

## Features / 功能

- PCL app name & icon (English: **PCL Mobile**, 中文: **PCL 定制版 手机端**)
- Offline/local accounts may download public Minecraft files
- Quick-join server (设置 → 杂项 → PCL)
- Auto-launch entry point (`PclAutoLaunchActivity`) for automation

## Multiplayer & interop (compliance first) / 联机与互通（合规优先）

- **Radmin VPN is NOT bundled**: it is proprietary Famatech software and Windows-only.
  PCL only detects/guides installation of the official build. / 不分发 Radmin，仅检测与引导安装。
- **Same-WiFi LAN**: uses Minecraft's built-in LAN discovery. / 同 WiFi 使用游戏自带 LAN。
- **Java ↔ Bedrock**: plan is Geyser (GPLv3) + Floodgate (MIT) local bridge, with source
  publication to satisfy GPLv3. / 计划用 Geyser + Floodgate 本地桥接，并同步发布源码。
- **NetEase / 网易**: only with official authorization/API; no protocol reverse engineering,
  no auth bypass. / 仅在官方授权/API 下实现，不逆向、不绕过登录。

## Build / 构建

Requires JDK 8 (toolchain) and Android SDK:
需要 JDK 8（toolchain）与 Android SDK：

```bat
gradlew.bat :app_pojavlauncher:assembleDebug
```

## Releases / 发布

Signed with `CN=PCL Pro Mobile`; APK published in GitHub Releases and mirrored in China.
使用 `CN=PCL Pro Mobile` 证书签名，APK 发布在 GitHub Releases 并提供国内镜像。

## Download / 下载

- GitHub: https://github.com/yangtianyi117-maker/PCL-Mobile/releases/tag/v0.2.0
- China mirror / 国内镜像: http://124.221.231.213/pclpro/PCL-Mobile-0.2.0.apk
- SHA-256: `987ADB9E116A41150A163DDF3D4273278721E554FBF95B8DC4C954F8AAA6FA87`

> Radmin VPN is not included. / 不含 Radmin VPN。