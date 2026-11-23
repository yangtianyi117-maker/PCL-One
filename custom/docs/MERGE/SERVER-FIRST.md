# 固定服务器方案（已采纳）

> 日期：2026-11-21　状态：服务器已执行，客户端已改并重新构建
> 结论：**手机和 PC 都直接进这台固定服务器**。二维码、中继守护、动态端口、客户端 Geyser 桥全部不做。

## 1. 最终架构

```
PC  ──(Java TCP)──▶ 124.221.231.213:25566 ──▶ vanilla 26.3
手机 ─(Bedrock UDP)─▶ 124.221.231.213:19132 ──▶ Geyser ──▶ vanilla 26.3
```

- 手机：在 Minecraft 里添加一次 `124.221.231.213:19132`，以后直接进；不需要 PCL、不需要扫码。
- PC：启动器「工具 → 联机 → 服务器」点「进入服务器」，用当前选中实例直连 `25566`。
- 房间码联机已不再是主路径：EasyTier 中继已停用，房间代码待下一步删除。

## 2. 服务器（124.221.231.213）

### 清理结果

| 项 | 前 | 后 |
|---|---|---|
| 可用内存 | 666 MB | **1694 MB** |
| 磁盘占用 | 11 GB | **9.5 GB** |
| 泰坦（4 个 unit + 数据 + cron） | 运行中，Xmx2300M | 已删（world/config 归档 7 天） |
| frps（0 客户端，仅被扫描） | 运行中 | 已删 |
| 监听端口 | 25565 / 25566 / 19132 / 7000 … | 25566 / 19132（+22/80/8088/11010） |

### 保留服务

| 服务 | 端口 | 作用 |
|---|---|---|
| `vanilla` | TCP 25566 | 原版 26.3 生存（max-players 12，view-distance 8，online-mode=false） |
| `geyser` | UDP 19132 / 19133 | 基岩入口 → 25566 |
| `easytier-relay` | — | **已停用并 disable**（房间体系下线） |
| `mcp-api` + `mcp-data` | 8088 | 宿主平台 API（产品数据，未动） |
| `nginx` | 80 | `/pclpro/` 更新通道 |

### 归档

`/home/ubuntu/_archive/`：`titan-1.7.10-2026-11-21.tar.gz`（43 MB）、`titan-1.12.2-world-2026-11-21.tar.gz`（3.7 MB），保留 7 天后删除。

## 3. 容量结论

- **内存**：删泰坦后剩余 1.7 GB，vanilla(≤1.28G) + Geyser(384M) 之外仍有约 1 GB 余量。
- **CPU**：2 vCPU（EPYC 7K62），当前 0 玩家、无 tick 警告；12 人时 CPU 是瓶颈而非内存。
- **带宽**：公网带宽未测，是真正的上限；超载时先降 `max-players`。

## 4. 客户端改动（构建 20 / `pclpro-2026-11-21.11`）

| 动作 | 内容 |
|---|---|
| 删除 | `GeyserBridge`、`PhoneShare`、`RakNetProbe`、`ProPhoneService`、`PhoneQrDialog`、`PhoneShareTest`、`QRCoder` 依赖 |
| 删除 | 手机卡片 5 个按钮、`LanIp`/`ResolvePhoneJavaPort`/`StartPhoneAsync`、自动起桥、`pclpro://phone` 深链分支 |
| 新增 | 配置 `LinkServerJava`（`124.221.231.213:25566`）、`LinkServerBedrock`（`124.221.231.213:19132`） |
| 新增 | 「服务器」卡片：Java 一键进入（`--quickPlayMultiplayer`）、基岩地址一键复制 |
| 删除 | 安装目录 `PCL\Geyser\Geyser-Standalone.jar`（59.6 MB） |

## 5. 验收清单

- [x] 服务器 25565 关闭、25566/19132 监听、`systemctl --failed` 无业务单元
- [x] 客户端 `dotnet build` 0 错误；`PCL.Core.Test` 编译通过
- [ ] 手机（蜂窝）连 19132 进服 —— 待真机
- [ ] PC「进入服务器」直连 25566 —— 按钮已在 UI 中验证，真机进服待测
- [x] `tools\verify-single-install.ps1` 12/12（build 19 / payload .10 实测通过）

## 6. 后续可选

1. 桌面 `泰坦PCL\`（1.7.10 客户端包）随服务器删除已失效，可整包删除。
2. 房间码 / 公开房 / EasyTier 是否保留：当前保留，若确认「都用服务器」可后续整体移除。

## 7. mcp-api 复查与退役（2026-11-21）

**判定：已不再使用。** 证据：

| 证据 | 内容 |
|---|---|
| 最后一次业务请求 | 2026-11-16 23:44（`/api/pack`），之后 5 天 0 业务请求 |
| 全部日志接口计数 | `/api/rooms` 296、`/api/host/log` 240、`/api/host` 189、`POST /api/host` 87、`/api/health` 92、`/v1/join` 29 —— 全部集中在 11/15–11/16 |
| 每日请求 | 11-16 5759 → 11-17 817 → 11-18 192 → 11-19 316 → 11-20 468 → 11-21 108；11/17 之后全部是 `/pclpro/`（启动器更新） |
| 数据冻结 | `rooms/public.json` 停在 11-17（demo 房间指向已删除的 25565）、`hosted/state.json` 停在 11-18 |
| 残留 | 5 个 defunct java 子进程；公网 `/api/host` 可远程起服（安全暴露面） |

**处理**：停用 + disable + 删 unit；归档 `/home/ubuntu/_archive/mcp-platform-2026-11-21.tar.gz`（721 KB，含代码/rooms/index/unit）；删 `/home/ubuntu/mcp-data`（2.1 GB）与 `/home/ubuntu/mcp`；nginx 去掉 `/api/`、`/r/`、`location = /` 的 8088 反向代理，`/` 回落到静态 `/var/www/mc/index.html`。

**连带修复**：`geyser.service` 原指向 `mcp-data/hosted/Geyser-Standalone.jar`。删除后从运行进程的文件描述符恢复同一 jar（62,469,550 B）到 `/home/ubuntu/geyser/`，改 unit 并重启。外部实测：19132 RakNet pong 正常、25566 TCP 可连、`/pclpro/update.json` 200。

**结果**：磁盘 9.5 GB → **7.5 GB**；可用内存约 **1.75 GB**；8088 关闭；运行中服务 22 → 18。
## 8. 启动时不再弹购买窗口（构建 21）

**根因**：`GameLaunch.McLaunchPrecheck` 每次启动都会调用 `_ShowPurchaseHint()`（无微软档案时），弹窗里的「购买」按钮打开 Xbox 商店页；另一处 `HandleMicrosoftNotOwnedError` 也会打开同一个商店链接。

**修复**：
- `_ShowPurchaseHint` 在 PCL Pro 构建直接返回（不弹窗、不开商店）。
- `HandleMicrosoftNotOwnedError` 在 Pro 构建只保留一个「确定」提示，不再打开商店。

**验证**：点「启动游戏」后 8 秒进入 Minecraft 标题界面，启动日志中没有任何 购买/商店/xbox 痕迹，也没有对话框出现。

**版本**：构建 21 / `pclpro-2026-11-21.12` / SHA-256 `6A402F3D45306522…`。
## 9. 服务器列表做成「模组列表」同构界面（构建 25）

**第一性**：联机入口的本质是「去哪台服务器」。一台服务器 = 一行资源，字段与模组条目同构：
图标、名称/副标题、标签、描述、版本/加载器、人数、延迟、来源。所以它不是「房间」，而是一个可以浏览的目录。

**实现**：
- 新增 `MyServerItem` 控件（仿 `MyCompItem`：50px 图标、标题/副标题、描述、元信息行、右侧「进入」「复制」按钮）。
- 新增 `ServerDirectory`：从 `LinkServerListUrl`（默认 `http://124.221.231.213/pclpro/servers.json`）拉取，失败回退内置固定服。
- 每行用 `McPingService` 实时 ping：人数 `0/12`、延迟 `98 ms`；点「进入」直接 `--quickPlayMultiplayer`，点复制拿基岩地址。
- 旧的「最近房间 / 公开房间 / 局域网房间 / 创建大厅」四张卡片已 `Collapsed`（代码保留，不再显示）。

**添加服务器**：改 `/var/www/mc/pclpro/servers.json`，字段：
`id / name / subtitle / description / tags[] / mc / loader / java / bedrock / source / icon`，客户端点「刷新」即生效。

**版本**：构建 25 / `pclpro-2026-11-21.16` / SHA-256 `48EC48768BE88BF8…`。