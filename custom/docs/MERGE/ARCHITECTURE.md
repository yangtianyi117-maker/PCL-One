# PCL Pro 合并架构

日期：2026-11-20

## 结论

- 唯一产品：**PCL Pro**，外壳与核心来自 `tools/pclce/PCL-CE-dev`（PCL CE 2.15.1）。
- 唯一数据根：`<exe目录>\PCL\`（PCL 便携模式）。
- 唯一房间身份：Scaffolding `U/XXXX-XXXX-XXXX-XXXX`。
- McLauncher 独立外壳已归档，不再构建。

## 启动器四职责

| 职责 | 实现 |
|---|---|
| 身份 | `PCL.Launcher.Core` 正版/离线认证 |
| 版本解析/安装 | `PCL.Launcher.Core` + `PCL.Core.Minecraft` |
| 启动 | `PCL.Launcher.Core/Launch`（`--quickPlayMultiplayer`） |
| 联机 | `PCL.Core.Link.*` |

## Link 模块

```
PCL.Core/Link/
  Session/      (LobbySession：房间数据 + LobbyController + 房间 CTS)
  Room/         LobbyCodeService（唯一房间码入口）、目录、心跳、最近房间
  EasyTier/     EasyTier 进程与端口转发
  Scaffolding/  PCL CE 联机协议（大厅码/端口/玩家/内容清单）
  Pack/         零整合包清单与物化
  Lan/          局域网报文、发现、应答、世界日志监听（从 McLauncher 移植）
  Phone/        Geyser 基岩桥、RakNet 探针、二维码、pclpro:// 深链
  Bridge/       ViaProxy 跨版本
  McPing/       服务器状态查询
```

## 房间模型

```
Room = 身份(U/ 码) + 端点(房主 MC 端口) + 路径(Lan | Relay)
     + 发现(Code | Directory | Lan) + 附属(Pack | Phone)
```

- `Relay`：EasyTier + Scaffolding，跨网。
- `Lan`：同网段 UDP 应答 + 直连，不启 EasyTier。
- `Directory`：`PCL.RoomIndex`（自建）或 ntfy 公共频道。
- `Pack`：房主发布签名清单，加入者自动物化实例。
- `Phone`：Geyser 把 Java 端口翻译成 Bedrock 19132。

## 应用层新增

| 文件 | 作用 |
|---|---|
| `Plain Craft Launcher 2/Link/ProLanService.cs` | 房主侧局域网应答生命周期 |
| `Plain Craft Launcher 2/Link/ProPhoneService.cs` | 手机桥启停与接入信息 |
| `Plain Craft Launcher 2/Link/ProDeepLink.cs` | `pclpro://` 命令行/RPC 路由 |
| `Plain Craft Launcher 2/Link/PhoneQrDialog.cs` | 二维码弹窗 |
| `PCL.Core/Link/Room/LobbyCodeService.cs` | 归一化/生成/旧码识别 |
| `PCL.Core/Link/Room/RecentRoomsService.cs` | 最近房间 + 旧数据导入 |
| `PCL.Core/Link/Lan/*` | 局域网协议/发现/应答/日志监听 |
| `PCL.Core/Link/Phone/*` | Geyser/探针/二维码/深链 |

## 与上游 PCL CE 的关系

- 固定 2.15.1；不自动跟随上游。
- Pro 补丁清单见 `PRO-PATCHES.md`。
- CE 自动更新在 Pro 构建中短路（`PCL.Core/App/ProEdition.cs`），避免覆盖。

## 当前状态

- `LobbySession` 已收口：状态、玩家、code、name、pack、`LobbyController`、房间 CTS 都在 `LobbyService.Current`。
- `PackJoinService` 已下沉到 `PCL.Launcher.Core/Link`；`ModInboxService` 已下沉到 `PCL.Launcher.Core/Minecraft/Mods`。
- `PclProBridgeServer` 仍是 app 层适配器：负责发现本地世界、拼 manifest、调用 `LobbyService`；这是 UI/游戏集成边界，不再复制大厅状态。
- 剩余是环境验收，不是代码缺口：双机同网段、手机 Bedrock 真机、Geyser 下载、游戏内 bridge mod 需要对应硬件/网络。
