# 合并验收记录

日期：2026-11-20

## 产物

- `dist\PCLPro.exe`：112.4 MB，SHA-256 `B39824BFFA31A005584BFAD43AFADD014E3503200D0006697C0C3D527660D8CF`
- payload 版本：`pclpro-2026-11-21.8`
- 悬浮“PCL Pro 建房”按钮已按用户要求删除（`PclProOverlay.cs` 已删除，启动日志不再出现 `[Overlay]`）
- 加入页新增“加入后手动进入（在‘多人游戏’列表里点，不自动进服）”：勾选后只建隧道并广播 LAN 条目，由玩家在原生“扫描局域网”界面点进
- 自动更新已上线：`http://124.221.231.213/pclpro/`，内置公钥验签、静默下载、替换 bootstrapper、提示重启
- 发版流程已内置：`release\release.cmd`（一键改版本→构建→签名→上传→历史记录）
- 桌面部署：`C:\Users\Administrator\Desktop\PCL Pro\PCLPro.exe`，与 `dist` 同 hash
- 备份：`PCL Pro.bak-2026-11-20`；旧 exe 另存 `PCLPro.exe.bak-2026-11-20`

## 已通过

| 项 | 结果 | 证据 |
|---|---|---|
| PCL CE 全量构建 | 0 错误（既有 nullable/未使用警告） | `artifacts/final-build.log` |
| 新增单元测试 | 20/20 通过 | `artifacts/final-tests2.log` |
| 房间码归一化 | 生成/解析/小写/空格/无连字符/旧码识别 | `LobbyCodeServiceTest` |
| 局域网报文 | 原版报文/应答/探测/网段计算 | `LanProtocolTests` |
| 手机载荷/二维码 | 往返解析/PNG 头 | `PhoneShareTest` |
| 深链解析 | `pclpro://join`、裸码、phone URL、拒绝垃圾 | `DeepLinkRouterTest` |
| 打包 exe 干净目录启动 | 自动解压运行时、窗口标题 `PCL Pro` | `build/smoke` 实测 |
| bootstrapper 参数透传 | 子进程命令行包含传入参数 | `Win32_Process.CommandLine` 实测 |
| 覆盖升级 | 已有运行时上版本化重解压成功，`PCL/` 数据保留 | 桌面 .2→.3→.4 实测 |
| 深链端到端 | `PCLPro.exe join-code --url ...` → `[深链] 收到房间邀请` | 日志 `Launch-2026-11-20-092627595.log` |
| 局域网服务启动 | `[LAN] 局域网应答服务已启动` | 日志 `Launch-2026-11-20-092756637.log` |
| 加入中退出竞态 | 已修：`LeaveLobbyAsync` 等待 join 收尾；取消路径不再级联 `10054`/重复弹窗 | `LobbyService.cs` |
| 残留 EasyTier 实例 | 已修：启动前只清理 PCL 目录下的 `easytier-core`，外部实例不动 | `EasyTierEntity.cs` |
| 签名更新包 | 正常包 PASS；改 exe→payload sha256 mismatch；改 manifest→signature invalid | `dist/PCLPro-update-2026-11-21.8-signed.zip` |
| 自动更新端到端 | `.9` 客户端真实升级到 `.10`：发现 → 下载 → 验签/哈希 → 替换 → 提示重启；重启后 payload `.10` | 日志 `Launch-2026-11-20-101751539.log` |
| 启动参数崩溃 | 修复 `_CollectJvmArguments` 空引用：实例未选中/JSON 不可读时自动选可用实例，否则给明确错误 | `Arguments.cs` |
| 实机启动 | `.13` 点击“启动游戏”后正常生成参数并启动 26.3-Fabric：`开始获取 Minecraft 启动参数` → `玩家用户名：123` → `26.3-Fabric 启动成功` | 日志 `Launch-2026-11-20-105616120.log` |
| 更新断点续传 | 本地 TCP 服务器首包声明 3 MB、只发 1 MB 后断开；客户端第 2 次用 `Range: bytes=1048576-` 续传，校验 SHA-256 后替换 | `tools/test/ProUpdateRetryTest`，`result=True same=True requests=2` |
| 中断续传实机 | `.14` 下载到 91 MB 被中断；重启后从断点续传完成，替换成功 | 日志 `Launch-2026-11-20-120312806.log` |
| 超时续传实机 | `.14→.15` 首次下载 3 分钟超时，自动 Range 续传并在 17 秒内替换成功 | 日志 `Launch-2026-11-20-120955619.log` |
| LobbySession 收口 | `LobbyService` 的 state/code/name/pack/isHost/players + `LobbyController` + 房间 CTS 统一到 `LobbySession Current`，静态属性改为兼容转发；构建 0 错误 | `LobbySession.cs` |
| 超时续传实机 2 | `.15→.16` 同样 3 分钟超时后续传成功，重启后 payload `.16` | 日志 `Launch-2026-11-20-121940575.log` |
| 中断续传实机 2 | `.16→.17` 下载中断在 12 MB，重启后续传完成 | 日志 `Launch-2026-11-20-202431994.log` |
| ModInbox 下沉 | `PclProModInbox` 从 app 移到 `PCL.Launcher.Core/Minecraft/Mods/ModInboxService.cs`，UI 只启停/打开目录；构建 0 错误 | `ModInboxService.cs` |
| PackJoin 下沉 | `PackJoinService` 移到 `PCL.Launcher.Core/Link`，WPF 安装器通过 `IPackInstanceInstaller` 注入 | `PackJoinService.cs` + `McInstallPackInstanceInstaller.cs` |
| 更新源 | `http://124.221.231.213/pclpro/` 返回 200；exe 117907456 字节 | VPS nginx `/var/www/mc/pclpro` |
| 一键发版 | `release\release.ps1 -Version 2026-11-21.8 -BuildNumber 17` 成功改版本、构建、签名、上传 | `artifacts/release-17.log` |
| CE 自动更新短路 | `[Update] PCL Pro 构建：自动更新已禁用` | 同上 |

## 基线说明

- 合并前 `PCL.Core.Test` 全量：241 通过 / 55 失败。失败集中在依赖 `Basics`/`metadata.json`/
  `EventBus` 初始化的用例（测试工程不加载 app 元数据），属于既有基线，不是本次改动引入。
- 本次新增测试不依赖这些初始化，全部通过。

## 未完成/未实测

| 项 | 原因 | 下一步 |
|---|---|---|
| 双机局域网零配置 | 本机回环不能真实模拟 AP 隔离/跨 VLAN | 两台同网段机器：一台开世界，另一台刷“局域网房间” |
| 手机 Bedrock 真机 | 需要真实手机 + Geyser 下载 | 点“启动基岩桥”，手机填 `IP:19132`；先用 RakNet 探针验证 |
| Geyser 下载 | 构建环境网络受限，未在本次运行中触发 | 首次点“启动基岩桥”时下载；失败只影响手机卡 |
| `pclpro://` 注册表 | 沙箱/权限可能拒绝写 HKCU | 在正常用户会话点“注册扫码直达” |
| 公开房目录/ntfy | 依赖外网 | 有网时点“刷新”验证 |
| 零整合包加入 | 依赖外网 Modrinth | 房主带 mod 建房，加入者无整合包加入 |
| bridge mod（游戏内按钮） | 需要 1.21.1 实例 | 用 `tools/modbuild/PCLProBridge-Fabric-1.21.1` 构建后实测 |

## 桌面部署

- 已备份：`C:\Users\Administrator\Desktop\PCL Pro.bak-2026-11-20`
- 新产物：`dist\PCLPro.exe`（SHA-256 见上）
- 部署时保留 `PCLProRuntime\PCL` 数据；bootstrapper 按 `.payload-version` 覆盖代码。
- 旧 `McLauncher.exe` 未删除；源码已归档到 `archive/mclauncher-2026-11-20/`。
- 首次启动会自动导入旧 McLauncher 最近房间；`ML-` 旧码不会被伪造转换。
