# PCL 定制版 相对 PCL2 的功能增量

> 说明：本文写于改名之前，文中「PCL Pro」是本项目当时的开发期代号，现在是 **PCL 定制版 / PCL Custom**。
>
> 对比对象：**PCL 定制版**（构建 `pclpro-2026-11-21.8`，BuildNumber 17）vs **PCL2**
> 对比口径：PCL Pro = 上游「PCL Community Edition 2.15.1」源码 + Pro 补丁。
> 因此本文把「官方 PCL2 主线的启动器内核」当作基线，只列 Pro 侧多出来的东西。
> 证据：源码逐文件 diff（80 个新增文件、49 个修改文件）、`docs/MERGE/*` 设计文档、运行日志。
> 表中「PCL2」一列 = 官方 PCL2 / 上游 CE 2.15.1 没有对应实现；判断依据是这些能力全部来自 Pro 新增文件，
> 上游树里查不到同名模块。

---

## 一句话结论

PCL2 的核心是「装游戏、开游戏」；PCL Pro 在保留这套内核的前提下，把**联机**做成了一整套系统
（局域网免码直连、公开房免口令、手机基岩接入、游戏内建房、零整合包同步、跨版本互通、自建房间索引），
再加上**模组收件箱**、**签名自更新**、**便携化与去弹窗**这几层工程化改造。

---

## 一、联机：Pro 的主要增量（PCL2 只有「纳塔亚克大厅 + 房间号」一条路）

| # | 能力 | PCL2 | PCL Pro | 实现 / 证据 |
|---|---|---|---|---|
| 1 | **局域网免房间码直连** | 无。只能建房拿码，或游戏里手动开 LAN | 同一网段直接看到对方房间并进入；房主只要在游戏里「对局域网开放」，PCL 自动识别端口 | `PCL.Core/Link/Lan/*`（`LanProtocol`/`LanDiscovery`/`LanResponder`/`LanWorldWatcher`）+ `ProLanService`；UDP 25566 自定义应答 `MCROOM1?` → `MCROOM1 <port> <0\|1> <name>`，组播过不去时逐地址扫描兜底 |
| 2 | **公开房列表（免口令）** | 无。必须知道房间号 | 「工具 → 联机」里列出公开房，点一下即加入 | `Link/Room/HttpRoomDirectory`、`NtfyRoomDirectory`、`RoomIndexClient`；UI 文案「点击房间即可加入，无需输入口令」 |
| 3 | **零配置公开房发现** | 无 | 不填服务器地址也能用房：默认走 `ntfy.sh` 公共 topic，可换自建 | `PCL.RoomIndex/README.md`；配置 `LinkNtfyPublicRooms` |
| 4 | **自建公开房索引服务** | 无（完全依赖第三方大厅） | 附带一个可直接部署的 ASP.NET Core + SQLite 服务 | `PCL.RoomIndex/`：`PUT /v1/rooms/{id}`、`GET /v1/rooms?mc=&loader=&q=&limit=`、`DELETE`、`POST .../report`、`/health` |
| 5 | **房主签名防伪** | 无 | 房主 ECDSA P-256 签名房间记录，客户端只认验签通过且未过期（90s）的记录 | `PCL.Link.Protocol/RoomRecord.cs`、`RoomSignature.cs`（`PCLPro.RoomRecord` 域） |
| 6 | **手机（基岩版）接入** | 无。手机端进不了 Java 房间 | 一键「启动基岩桥」：内置 Geyser Standalone 把 Java 端口翻译成 Bedrock `19132`，手机直接连 | `PCL.Core/Link/Phone/GeyserBridge.cs`（`transport: both`、`auth-type: offline`）+ `ProPhoneService` |
| 7 | **扫码 / 复制接入信息** | 无 | 「二维码」「复制接入信息」把房间码+网络名+密钥+房主虚拟 IP+端口压成 `pclpro://phone?...` 载荷 | `Phone/PhoneShare.cs`（QRCoder 生成 PNG）、`PhoneQrDialog.cs` |
| 8 | **pclpro:// 深链** | 无 | 注册 `HKCU\Software\Classes\pclpro`，点链接/扫码直接落到联机页输入框；命令行 `join-code --url` 也支持 | `Phone/DeepLinkRouter.cs`、`ProDeepLink.cs`、`StartupService` 的 `join-code` 子命令、`SingleInstanceService` RPC 转发 |
| 9 | **游戏内建房按钮** | 无 | Fabric 小 mod：多人游戏界面底部加「PCL Pro 建房」，点一下调本机桥开房 | `tools/modbuild/PCLProBridge-Fabric-1.21.1`（mixin `MultiplayerScreen`）+ 启动器侧 `PclProBridgeServer`（`127.0.0.1:60172`，token 鉴权，`/v1/ping`、`/v1/create`） |
| 10 | **零整合包联机** | 无。缺 mod 就对不上 | 房主建房时把 `mods/`、`config/` 生成**签名清单**随房发布；加入者自动装 MC/加载器、按 SHA-512 从 Modrinth 反查下载 mod、写配置，物化成受管实例 | `PCL.Core/Link/Pack/*`（`PackManifest`/`PackProvisioner`/`ModrinthPackSourceResolver`）+ `PackJoinService`；开关 `PCL_PRO_PACK_SYNC` |
| 11 | **跨版本互通** | 无。版本不同基本进不去 | 仅 vanilla 房：加入者本地起 ViaProxy 翻译协议，白名单 10 个热门版本（1.20.1→26.3） | `PCL.Core/Link/Bridge/ViaProxy*` + `PageToolsGameLink.TryLaunchViaProxyAsync` |
| 12 | **最近房间 / 旧数据迁移** | 无 | 记住最近房间，双击回填；首次启动导入旧 McLauncher 的房间记录（旧 `ML-` 码只读展示） | `Link/Room/RecentRoomsService.cs`、`FormMain` 启动导入 |
| 13 | **加入后手动进入** | 无 | 勾选后只建隧道 + 广播 LAN 条目，由玩家在原生「多人游戏」列表点进，不强制自动进服 | UI「加入后手动进入」+ `PageToolsGameLink` |

## 二、模组与实例

| # | 能力 | PCL2 | PCL Pro | 实现 |
|---|---|---|---|---|
| 14 | **模组收件箱（拖 jar 即装）** | 无 | 把 jar 丢进 `PCL\ModInbox` 自动识别加载器/MC 版本并装进当前实例；缺 `required` 依赖自动从 Modrinth 补齐；成败分 `done/`、`failed/` | `ModInboxService.cs`、`ModJarInstaller.cs`、`ModJarInspector.cs`、`ModrinthDependencyResolver.cs`；UI 另有「选择 jar 安装」「打开收件箱」 |
| 15 | **入房自动建实例** | 无 | 加入别人的房时按清单自动准备实例并启动，不用先收整合包 | `McInstallPackInstanceInstaller.cs`（WPF 安装器适配 `IPackInstanceInstaller`） |

## 三、更新与分发（PCL2 走官方更新器；Pro 自成一套）

| # | 能力 | PCL2 | PCL Pro | 实现 |
|---|---|---|---|---|
| 16 | **签名自动更新** | 官方发布渠道 | 内置 P-256 公钥 + manifest 验签 + exe SHA-256 校验，通过才替换 bootstrapper；只换启动器外壳，运行时与 `PCL/` 数据不动 | `PCL.Core/Updates/ProUpdateService.cs`、`ProUpdateRunner.cs`；通道 `http://124.221.231.213/pclpro/` |
| 17 | **断点续传** | — | `Range` 续传，3 分钟超时/中断后可接着下（验收记录里有多次实机续传） | `ProUpdateService` + `docs/MERGE/ACCEPTANCE.md` |
| 18 | **版本化运行时解压** | — | `.payload-version` 决定是否重新解压运行时，覆盖升级保留 `PCL/` 数据 | `tools/bootstrapper/main.zig` |
| 19 | **一键发版流水线** | — | 改版本 → 构建 → 签名 → 上传 → 记 HISTORY | `release/release.ps1`、`tools/sign-pclpro-update.ps1`、`publish-pclpro-update.ps1`、`release/HISTORY.md` |

## 四、体验与合规开关（PCL2 里是固定的）

| # | 变化 | 说明 |
|---|---|---|
| 20 | **便携模式** | 所有可写数据放 `exe 同目录\PCL\`，不碰 `%AppData%`（`PCL.Core/App/Paths.cs`） |
| 21 | **去掉启动弹窗** | 不再弹 EULA 同意、遥测同意、社区版提示、缺 WebP 组件包提示；确认框直接按「继续」走（`FormMain`、`DependencyCheckService`、`DialogCompat`） |
| 22 | **品牌** | 标题/关于页统一显示 `PCL Pro`，不再显示 CE 角标 |
| 23 | **地区限制开关** | 默认仍遵守「大陆地区禁用 Minecraft 安装」，但提供 `ProDisableRegionRestriction`（默认关，需用户主动开，自担风险）；对应环境变量 `PCL_PRO_DISABLE_REGION_RESTRICTION=1` |
| 24 | **免微软验证建档** | 离线/第三方档案可直接建，不强制先做微软登录 |
| 25 | **稳定性修补** | 启动参数收集空引用（实例未选中/JSON 损坏时自动选可用实例）、EasyTier 残留进程清理、加入中退出的竞态修复 |

## 五、配套件

| # | 东西 | 说明 |
|---|---|---|
| 26 | **手机端 APK** | `PCL Pro 手机版`：同 Wi-Fi 下扫 /24 网段 + 监听 Bedrock 组播 `224.0.2.60:4445`，显示 MOTD/版本/人数/延迟，一键复制 `IP:19132` 并拉起 Minecraft；零第三方依赖 |
| 27 | **单元测试** | 新增 20 个测试覆盖房间码归一化、局域网报文、手机载荷/二维码、深链解析、清单/依赖/物化 |

## 六、和 PCL2 一样的能力（不构成差异）

启动器内核沿用上游：正版/离线登录、版本安装与修复、Forge/Fabric/NeoForge/Quilt/OptiFine/LiteLoader 安装、
整合包导入导出（含 MultiMC）、模组/资源包/光影管理、皮肤与披风、存档管理、崩溃分析、内存设置、下载源与代理、
音乐/截图等。这些是 PCL2 与 PCL Pro 的共同底座。

## 七、边界与未实测（据验收记录）

- 跨版本互通**只对 vanilla 房**生效，且两端版本都要在白名单里；带 mod 的房仍要求同版本同加载器。
- 游戏内建房 mod 目前只编了 **1.21.1**；26.3 需在「工具 → 联机」页建房。
- 双机真实局域网（AP 隔离/跨 VLAN）、手机 Bedrock 真机、Geyser 首次下载、`pclpro://` 注册表写入、
  公开房目录（依赖外网）在验收时均标记为「未实测」，需真实环境验证。
- 公开房频道是公开的：不做账号/审核，房间口令也是公开的；只靠房主签名保证「记录没被篡改」。
- Pro 固定在上游 2.15.1，且**短路了 CE 自动更新**，上游新功能不会自动合入。

---

## 附：补丁规模

| 项 | 数量 |
|---|---|
| 相对上游新增文件 | 80（含测试） |
| 相对上游修改文件 | 49 |
| 删除的上游文件 | 0 |
| 新增依赖 | `QRCoder`（二维码） |
| 新增子项目 | `PCL.Link.Protocol`、`PCL.RoomIndex`、`PCL.RoomIndex.Tests` |
