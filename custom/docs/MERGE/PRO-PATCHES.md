# Pro 相对 PCL CE 2.15.1 的补丁清单

> 目的：升级/对照上游时，先看这份清单，避免把 Pro 能力覆盖掉。

## 新增文件

### PCL.Core

- `App/ProEdition.cs`
- `Link/Room/LobbyCodeService.cs`
- `Link/Room/RecentRoomsService.cs`
- `Link/Lan/LanProtocol.cs`
- `Link/Lan/LanDiscovery.cs`
- `Link/Lan/LanResponder.cs`
- `Link/Lan/LanWorldWatcher.cs`
- `Link/Phone/GeyserBridge.cs`
- `Link/Phone/RakNetProbe.cs`
- `Link/Phone/PhoneShare.cs`
- `Link/Phone/DeepLinkRouter.cs`
- `Updates/ProUpdateService.cs`
- `Minecraft/Mods/ModInboxService.cs`（从 app 层迁入）
- `Link/PackJoinService.cs`（从 app 层迁入）
- `Link/Lobby/LobbySession.cs`

### PCL.Core.Test

- `Link/Room/LobbyCodeServiceTest.cs`
- `Link/Lan/LanProtocolTests.cs`
- `Link/Phone/PhoneShareTest.cs`

### app

- `Plain Craft Launcher 2/Link/ProLanService.cs`
- `Plain Craft Launcher 2/Link/ProPhoneService.cs`
- `Plain Craft Launcher 2/Link/ProDeepLink.cs`
- `Plain Craft Launcher 2/Link/ProUpdateRunner.cs`
- `Plain Craft Launcher 2/Link/PhoneQrDialog.cs`
- `Plain Craft Launcher 2/Link/McInstallPackInstanceInstaller.cs`（WPF 安装器适配）

## 修改的上游文件

| 文件 | 修改 |
|---|---|
| `PCL.Core/App/Config.cs` | `Link` 组新增 `LanEnabled/LanPort/PhonePort/PhoneAutoStart/DeepLinkEnabled/ProUpdateUrl/ProUpdateAuto` |
| `PCL.Core/App/ProEdition.cs` | 新增 `BuildNumber/BuildId`，供签名更新比较 |
| `PCL.Core/Link/Scaffolding/ScaffoldingFactory.cs` | 客户端码解析改走 `LobbyCodeService` |
| `PCL.Core/Link/Scaffolding/Server/ScaffoldingServerContext.cs` | 建房码改走 `LobbyCodeService.Generate` |
| `PCL.Core/Link/Lobby/LobbyService.cs` | 成功建房/加入后记录最近房间；加入/退出竞态修复（Leave 等 join、取消不弹错、网络错误友好提示） |
| `PCL.Core/Link/Scaffolding/EasyTier/EasyTierEntity.cs` | 启动前清理 PCL 目录下残留的 `easytier-core`，避免端口/中继冲突 |
| `PCL.Core/App/Essentials/StartupService.cs` | 新增 `join-code` 子命令 |
| `PCL.Launcher.Core/Updates/UpdateManager.cs` | Pro 构建短路自动更新 |
| `PCL.Launcher.Core/Launch/Arguments.cs` | 启动前解析实例：未选中/JSON 不可读时自动选可用实例，避免空引用崩溃 |
| `Plain Craft Launcher 2/Program.cs` | 启动时注册深链回调 |
| `Plain Craft Launcher 2/FormMain.xaml.cs` | 启停 `ProLanService`、深链 Flush、旧数据导入、`ProUpdateRunner` 启动 |
| `Plain Craft Launcher 2/Pages/PageTools/PageToolsGameLink.xaml` | 局域网房间、最近房间、手机基岩桥、注册深链 |
| `Plain Craft Launcher 2/Pages/PageTools/PageToolsGameLink.xaml.cs` | 对应事件与手机/局域网启动编排 |
| `Plain Craft Launcher 2/Pages/PageTools/PageToolsGameLinkViewModel.cs` | 局域网/最近房间数据 |
| `PCL.Core/PCL.Core.csproj` | 新增 `QRCoder` 包 |

## 构建/打包

- `tools/bootstrapper/main.zig`：参数透传 + 版本化暂存解压（覆盖升级保留 `PCL/` 数据，EasyTier 被占用时跳过）。
- `tools/build-pclpro.ps1`：publish → payload.zip → Zig bootstrapper。
- `tools/sign-pclpro-update.ps1`：ECDSA 签名 manifest + 签名包。
- `tools/publish-pclpro-update.ps1`：签名并上传到 `124.221.231.213:/var/www/mc/pclpro/`。
- `PCLProRuntime/.payload-version`：当前 `pclpro-2026-11-21.8`。

## 升级上游时的检查

1. `LobbyCodeGenerator` 是否仍是 `U/` 格式；如果是，`LobbyCodeService` 无需改。
2. `LobbyService` 的 `CreateLobbyAsync/JoinLobbyAsync` 签名是否变化；最近房间 hook 要跟着走。
3. `StartupService` 的 `SubcommandDefinition` 是否重构；`join-code` 要保留。
4. `UpdateManager.ScheduleBasedOnConfig` 是否改名；Pro 短路要保留。
5. `PageToolsGameLink` 是否重写；局域网/最近房间/手机卡片要重新接。
6. `PclProBridgeServer`/`ModInboxService` 的启动点仍在 `FormMain`；如上游改了生命周期，跟随。

## 2026-11-22 联机补丁（A/B/C）

### 新增文件

- `PCL.Core/Link/Bedrock/BedrockPing.cs`：RakNet 未连接 Ping（桌面显示基岩服状态）。
- `PCL.Core/Link/Bedrock/GeyserProvisioner.cs`：Geyser standalone 下载/校验/配置（`transport: both`，19132 RakNet + 19133 NetherNet）。
- `Plain Craft Launcher 2/Link/ProBedrockBridgeService.cs`：同 Wi-Fi 基岩桥：盯 `latest.log` 的 `Published LAN server on port N`，启动/停止 Geyser；支持自动启动等待实例加载。

### 修改

- `PCL.Core/App/Config.cs`：新增 `LinkBedrockBridgePort`、`LinkBedrockBridgeAutoStart`、`LinkBedrockBridgeJavaPath`。
- `PCL.Core/Link/Scaffolding/EasyTier/EasyTierEntity.cs`：房间路径显式注入自建中继 `tcp://124.221.231.213:11010`；host 白名单加 19132/19133。
- `Plain Craft Launcher 2/Pages/PageTools/PageToolsGameLink.xaml(.cs)`：手机进服卡、同 Wi-Fi 基岩桥卡、取消创建/加入/局域网卡 Collapsed、复制手机邀请按钮。
- `PageToolsGameLinkViewModel.cs`：官方 Bedrock 状态、基岩桥状态。
- `FormMain.xaml.cs`：自动启动基岩桥（配置开关）、退出停止。
- `tools/bootstrapper/main.zig`：payload 版本 `pclpro-2026-11-22.8`。

### 手机端（PCL Pro 手机版 1.1.0）

- 新增 `OfficialServers.java`、`JavaPing.java`。
- `MainActivity.java`：官方服卡、房间码解析/EasyTier 配置、`pclpro://` 深链。
- `Prefs.java`：官方地址与房间网络/密钥/虚拟 IP。
- `AndroidManifest.xml`：`pclpro://` intent filter。
- `build.ps1`：1.1.0 / versionCode 2。

### Pojav（次级）

- `Tools.java`：`PENDING_JOIN` + `--server/--port`（一次性）。
- `LauncherActivity.java`：`pcl_join` extra 自动启动。
- `catalog/CatalogActivity.java`：Java 服务器行「进入」。
- 构建注意：需 JDK 8 工具链、ASCII 路径；本机用 `.tmp/pojav-src-lead-catalog` 增量构建绕过 AAPT2 的 9-patch/中文路径问题。
