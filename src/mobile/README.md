# PCL Pro 手机版（Android）

> 与 PCL Pro 桌面端配套的联机助手：进官方固定服务器、同 Wi-Fi 发现 PCL 基岩桥、用房间码加入 EasyTier 房间。

![icon](dist/icon-512.png)

- 包名：`com.pclpro.mobile`
- 版本：`2.1.0 (versionCode 4)`
- 安装包：`dist/PCLProMobile-2.1.0.apk`
- 最低系统：Android 7.0（API 24），目标 API 34
- 依赖：**零第三方依赖**（无 AndroidX / 无 Gradle），仅用 Android Framework

## 2.1.0 联机能力（离线指南）

| 能力 | 说明 |
|---|---|
| 地球服 | 手机 Bedrock `124.221.231.213:19132`，与 PC Java `124.221.231.213:25566` 同一张 1:1000 地球地图；显示在线人数/延迟 |
| 地球指南 | 卡片新增「地球指南（规则 / 坐标）」按钮，打开 `/pclpro/earth.html` |
| 一键进入 | `minecraft://connect?...` 深链；失败回退打开 Minecraft；最后回退复制地址 |
| 局域网 | 原 /24 扫描 + Bedrock 广播；PC 启动基岩桥后同一 Wi-Fi 可发现 |
| 房间码 | 输入 `U/XXXX-XXXX-XXXX-XXXX`，派生 EasyTier 网络名/密钥，复制配置、打开 EasyTier、进 `10.114.51.41:19132` |
| 邀请深链 | `pclpro://join?c=...&n=...&s=...&h=...&p=...` 自动填房间页 |

完整三条链路（A 固定服 / B 同 Wi-Fi / C 房间码）见 [docs/MULTIPLAYER.md](docs/MULTIPLAYER.md)。

## 安装

```powershell
adb install -r dist\PCLProMobile-2.1.0.apk
```

或把 APK 传到手机，允许「安装未知来源应用」后点击安装。签名与 1.0.0 相同，可直接覆盖升级。

首次使用：手机与电脑同一 Wi-Fi → 打开 App → 官方服「进入」，或「扫描局域网」→ 选中房间 →「进入」。

## 技术要点

| 模块 | 说明 |
|---|---|
| `RakNetPing` | Bedrock 未连接 Ping：`0x01`（时间 + 离线魔数 + guid）→ `0x1C` |
| `JavaPing` | Java 版 Server List Ping（1.7+），显示 PC 地址状态 |
| `ServerScanner` | /24 并发探测 + `MulticastSocket` 加入 `224.0.2.60:4445` |
| `NetUtil` | `NetworkInterface` 枚举本机 IPv4，生成待探测主机列表 |
| `Ui` / `IconView` | 纯代码绘制 PCL 风格卡片、按钮、图标，无 XML 布局、无图片依赖 |
| `Prefs` | SharedPreferences 保存端口/超时/房间码/官方地址 |
| `pclpro://` | 房间邀请深链，导入房间码/EasyTier 网络/密钥/虚拟地址 |

## 构建

`aapt2 + javac + d8 + zipalign + apksigner`，不依赖 Gradle：

```powershell
cd 'C:\AndroidDev\pclpro-mobile'   # 中文路径的 ASCII junction
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

产物：`dist\PCLProMobile-2.1.0.apk`，签名密钥在 `keystore\pclpro-release.jks`（口令 `pclpro2026`，仅本地分发）。

> aapt2 不支持含中文的工程路径；构建时通过 `C:\AndroidDev\pclpro-mobile` 这个 ASCII junction 进入工程。

## 目录

```text
PCL Pro 手机版/
├─ app/                       # 2.1.0 联机助手（本 README 的主体）
├─ mobile-fork/               # PojavLauncher 分支（Java 版，次级）
├─ java-launcher/             # Java 启动器实验（P0/P1）
├─ tools/                     # PingTest、假 Bedrock 服务器、图标脚本
├─ build.ps1                  # 无 Gradle 构建
├─ dist/                      # APK、图标
├─ docs/                      # 联机说明、客户端 Mod/服务器目录、Java 启动器记录
└─ keystore/                  # 本地签名密钥
```

## 边界

- 本 APK 只做「发现 + 拉起/复制地址 + 房间邀请」，不内置 Minecraft、不分发游戏本体；
- 官方服手机走 Bedrock（Geyser 协议翻译），不是手机跑 Java；
- 房间码跨网络需要 EasyTier 中继可达；中继不可达时请用固定服务器或同 Wi-Fi；
- Pojav 分支可跑 1.21.x Java 版，26.3 需要 JRE 25，不在本 APK 的承诺内。

## 验证

- 模拟器：官方卡显示 `在线 · 0/12 人 · 58 ms`，PC `26.3 0/12`；
- 深链：`pclpro://join?...` 导入后房间页显示网络/密钥/`10.114.51.41:19132`；
- 构建：versionCode 3、同签名可覆盖；详见 [docs/MULTIPLAYER.md](docs/MULTIPLAYER.md)。
