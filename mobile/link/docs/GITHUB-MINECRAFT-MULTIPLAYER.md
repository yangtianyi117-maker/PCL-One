# GitHub Minecraft 联机项目检索报告

日期：2026-11-22 · 方法：GitHub Search API，18 组关键词（中英文），去重后 187 个仓库，人工过滤掉语言包/光影/数据包/棋牌垃圾后整理。

> 关键词：`minecraft lan`、`minecraft 联机`、`我的世界 联机`、`minecraft p2p`、`minecraft tunnel`、`minecraft frp`、`easytier`、`zerotier`、`sakurafrp`、`geyser`、`floodgate`、`bedrock java proxy`、`hmcl 联机`、`pcl2 联机` 等。

---

## 一、Minecraft 专用联机 / 局域网工具

| 项目 | 星 | 语言 | 许可 | 说明 | EXE |
|---|---:|---|---|---|---|
| [Xocio/Campfire](https://github.com/Xocio/Campfire) | 417 | C# | GPL-3.0 | 先进 Minecraft 联机工具箱（房间、P2P、工具集） | ✅ `Campfire.exe` |
| [MSLTeam/MSL](https://github.com/MSLTeam/MSL) | 235 | C# | GPL-3.0 | WPF 联机/开服工具，Windows 专用 | 源码/WPF，可编译 |
| [MCZLF/MinecraftConnectTool](https://github.com/MCZLF/MinecraftConnectTool) | 30 | C# | 未标 | P2P 穿透 + 中继 + EasyTier 三模联机工具箱 | 需确认 |
| [DeveloperKubilay/Safra](https://github.com/DeveloperKubilay/Safra) | 67 | Java | 未标（自定义） | 无服务器 P2P 联机，Fabric/Forge/NeoForge mod | ❌ jar |
| [SimpleServer/SimpleServer](https://github.com/SimpleServer/SimpleServer) | 129 | Java | MIT | Minecraft 服务端隧道（已归档） | ❌ |
| [dmitrymodder/minewire](https://github.com/dmitrymodder/minewire) | 278 | Go | MIT | 伪装成 MC 服务器的加密隧道（已归档） | 服务端二进制 |
| [Corona-Studio/ConnectX](https://github.com/Corona-Studio/ConnectX) | 100 | C# | MIT | 跨平台 P2P 联机库，ZeroTier SDK 驱动 | ❌ 库 |
| [minekube/connect](https://github.com/minekube/connect) | 64 | Go | Apache-2.0 | 托管边缘代理，任意 MC 服务器互联 | CLI |
| [Ragalikx/steam-bridge-mc](https://github.com/Ragalikx/steam-bridge-mc) | 58 | Java | MIT | 用 Steam P2P/SDR 联机，无需端口映射 | ❌ jar |
| [Koud-Wind/Netease-minecraft-LAN-connects-to-Server](https://github.com/Koud-Wind/Netease-minecraft-LAN-connects-to-Server) | 61 | Java | GPL-3.0 | 把网易版本地联机流量导向指定服务端 | zip |
| [pmh1314520/MCTier](https://github.com/pmh1314520/MCTier) | 1154 | TypeScript | 未标 | 虚拟局域网联机工具（全 LAN 游戏通用） | 需确认 |

## 二、底层组网 / 内网穿透（可被启动器调用）

| 项目 | 星 | 许可 | 说明 |
|---|---:|---|---|
| [EasyTier/EasyTier](https://github.com/EasyTier/EasyTier) | 14888 | LGPL-3.0 | 去中心化 mesh VPN，WireGuard 兼容，官方提供 Android APK |
| [EasyTier/EasytierGame](https://github.com/EasyTier/EasytierGame) | 332 | Apache-2.0 | 基于 EasyTier 的简易游戏联机 UI |
| [EasyTier/easytier-manager](https://github.com/EasyTier/easytier-manager) | 313 | AGPL-3.0 | 组网/内网穿透管理器 |
| [Musixal/Easy-Mesh](https://github.com/Musixal/Easy-Mesh) | 340 | 未标 | 基于 EasyTier 内核的 P2P VPN 脚本 |
| [ceocok/onekeyeasytier](https://github.com/ceocok/onekeyeasytier) | 218 | 未标 | EasyTier 一键组网脚本 |
| [qteasytier/qt-easy-tier](https://github.com/qteasytier/qt-easy-tier) | 261 | LGPL-3.0 | Qt 版 EasyTier 组网工具 |
| [Teleseon/cf-workers-et-ws](https://github.com/Teleseon/cf-workers-et-ws) | 99 | MIT | Cloudflare Workers 上跑 EasyTier WS |
| ZeroTier / n2n / WireGuard / frp | — | BSL / GPL / GPL / Apache-2.0 | 通用组网与穿透 |

## 三、Java ↔ 基岩互通

| 项目 | 星 | 许可 | 说明 |
|---|---:|---|---|
| [GeyserMC/Geyser](https://github.com/GeyserMC/Geyser) | 5978 | **MIT** | 基岩客户端连 Java 服务端的标准桥 |
| [GeyserMC/Floodgate](https://github.com/GeyserMC/Floodgate) | 703 | MIT | 基岩玩家免 Java 正版登录进入 |
| [GeyserMC/Hydraulic](https://github.com/GeyserMC/Hydraulic) | 323 | MIT | Geyser 伴生 mod，支持模组服 |
| [GeyserMC/GeyserConnect](https://github.com/GeyserMC/GeyserConnect) | 244 | MIT | 基岩直连任意 Java 服务器（托管） |
| [minekube/gate](https://github.com/minekube/gate) | 1091 | Apache-2.0 | 高性能 Bedrock & Java 多版本网络代理 |
| [DragonetMC/DragonProxy](https://github.com/DragonetMC/DragonProxy) | 981 | GPL-3.0 | 老牌基岩→Java 代理（已不活跃） |
| [THEREALWWEFAN231/TunnelMC](https://github.com/THEREALWWEFAN231/TunnelMC) | 180 | GPL-3.0 | Java 客户端连基岩服务器 |
| [BarrelMC/Barrel](https://github.com/BarrelMC/Barrel) | 55 | MIT | Java 连基岩（原型） |
| [Blackjack200/Ouranos](https://github.com/Blackjack200/Ouranos) | 60 | MIT | 基岩多版本协议代理 |
| [jhead/phantom](https://github.com/jhead/phantom) | 696 | MIT | 让 Xbox/PS 连自建 Java 服务器 |

## 四、启动器 / 开服器（带联机能力）

| 项目 | 星 | 许可 | 说明 | EXE |
|---|---:|---|---|---|
| [HMCL-dev/HMCL](https://github.com/HMCL-dev/HMCL) | — | GPL-3.0 | 支持联机/多人入口的启动器 | ✅ `HMCL-*.exe` |
| [Meloong-Git/PCL](https://github.com/Meloong-Git/PCL) | — | 自定义许可 | PCL2 本体 | 源码 |
| [SeaLantern-Studio/SeaLantern](https://github.com/SeaLantern-Studio/SeaLantern) | 423 | AGPL-3.0 | 社区共创开服器 | — |

## 五、EXE 发行物（Release 里确认含 exe）

- `Campfire.exe` — Xocio/Campfire（GPL-3.0）
- `HMCL-*.exe` — HMCL（GPL-3.0）
- MSL 为 WPF 项目，可自行编译 exe（GPL-3.0）
- MCTier / MinecraftConnectTool 需在各自 Release 页确认安装包格式

> 未发现 Radmin 相关开源项目；Radmin VPN 本身是 Famatech 闭源商业软件，不在可复用范围。

---

## 六、给 PCL 的合规复用建议

### 手机端（PCL-Mobile，GPLv3）

- ✅ **Geyser（MIT）+ Floodgate（MIT）**：可作为可选组件，运行本地桥接；许可最干净。
- ✅ **EasyTier（LGPL-3.0）**：以**独立进程/独立 APK** 方式调用，不静态链接，满足 LGPL。
- ✅ **ConnectX（MIT）**：P2P 联机库，可直接借鉴/集成。
- ✅ **minekube/connect（Apache-2.0）**：边缘代理方案。
- ⚠️ GPL-3.0 项目（Campfire/MSL/TunnelMC）：手机端本身就是 GPLv3，可集成，但需开源修改。
- ❌ 网易 LAN 重定向（GPL-3.0）：技术上可行，但**违反网易用户协议风险高**，不做。

### 桌面端（PCL 衍生，许可受限）

- 优先**调用外部进程**：EasyTier CLI、Geyser/Floodgate 独立 Java 进程；
- 不把 GPL-3.0 代码静态链接进 PCL 本体（PCL 的许可不是 GPL）；
- Radmin 仍只做“检测/引导安装”，不分发。

### 推荐 POC（最小可行）

```
PCL UI（联机页）
  ├─ 一键组网：启动 EasyTier 子进程（房间名/密码）
  ├─ 一键桥接：启动 Geyser + Floodgate（可选，基岩互通）
  └─ 服务器列表：写 servers.dat / 直连地址
```

这样既满足“一键局域网联机 + Java/基岩互通”，又只依赖 MIT/LGPL/Apache 组件，法律风险最低。

---

## 七、参考搜索入口

- GitHub 搜索：<https://github.com/search?q=minecraft+%E8%81%94%E6%9C%BA&type=repositories>
- EasyTier：<https://github.com/EasyTier/EasyTier>
- Geyser：<https://github.com/GeyserMC/Geyser>
- HMCL：<https://github.com/HMCL-dev/HMCL>
