# PCL Pro 联机落地说明（A/B/C/D）

> 2026-11-23：主服已升级为 **PCL Earth 地球服**（EarthMC 1:1000），地址不变：Java `124.221.231.213:25566`，Bedrock `124.221.231.213:19132`；手机 App 2.0.0 的「地球服」卡片直接进服。地图/规则/许可见 `Desktop\PCL Earth\docs`。

> 日期：2026-11-22　范围：PCL Pro 桌面端 + PCL Pro 手机版 1.0 联机助手（+ Pojav 次级 APK）
> 结论：A（固定服务器）双端落地并实测；B（同 Wi-Fi 基岩桥）代码完成且本机跑通 Geyser；C（房间码 P2P）桌面/手机房间卡与深链完成，跨网依赖 EasyTier 中继；D（Pojav Java）源码补丁与 APK 完成，26.3 受 JRE 25 限制。

## 0. 三条联机链路

| 链路 | PC | 手机 | 是否需要公网 | 版本要求 |
|---|---|---|---|---|
| A. 固定服务器 | Java `124.221.231.213:25566` | Bedrock `124.221.231.213:19132`（Geyser） | 是 | PC 26.3；手机基岩版（Geyser 兼容） |
| B. 同 Wi-Fi 基岩桥 | 游戏内「对局域网开放」+ 启动 Geyser | 扫描/手动填 PC 的 `IP:19132` | 否（同一 Wi-Fi） | PC 任意；手机基岩版 |
| C. 房间码 P2P | `U/` 码 + EasyTier + 基岩桥 | EasyTier 加入同网络 → `10.114.51.41:19132` | 需要中继（自建/公共） | 同 B，且需 EasyTier |
| D. Pojav Java | — | Pojav 一键进 1.21.x Java 服务器 | 视服务器 | 手机 JRE 17/21；26.3 需 JRE 25 |

## 1. 桌面端（PCL 定制版 / PCLPro.exe）

### 1.1 服务器列表（A）
「工具 → 联机」顶部「服务器列表」从 `http://124.221.231.213/pclpro/servers.json` 拉取；官方行显示 Java 人数/延迟，「进入」用当前 26.3-Fabric 实例直连 `25566`。

### 1.2 手机进服（A）
「手机进服（基岩版）」卡片显示 `124.221.231.213:19132` 的实时 RakNet 状态、Bedrock 版本、MOTD；按钮：
- 复制手机地址；
- 刷新状态；
- 打开手机版说明。

### 1.3 同 Wi-Fi 基岩桥（B）
「同 Wi-Fi 手机进服（基岩桥）」卡片：
1. 先在游戏里「对局域网开放」；
2. 点「启动基岩桥」：首次下载 `Geyser-Standalone.jar` 到 `PCL\Geyser\`（约 62 MB，需 Java 21+）；
3. 服务盯当前实例 `logs/latest.log` 的 `Published LAN server on port N`，自动用 Geyser 把 `127.0.0.1:N` 翻译到 `0.0.0.0:19132`；
   Geyser 配置为 `transport: both`：经典 RakNet 走 UDP 19132，1.26.51+ 的 NetherNet 走 UDP 19133 + TCP 19132；
4. 手机同一 Wi-Fi 扫描/填 `PC-IP:19132` 进入；
5. 关闭世界/游戏/启动器会停 Geyser。

配置键（`PCL\Shared\config.v1.json`）：
- `LinkBedrockBridgePort`（默认 19132）
- `LinkBedrockBridgeAutoStart`（默认 false）
- `LinkBedrockBridgeJavaPath`（留空自动挑 21+）

### 1.4 房间码（C）
创建/加入房间卡片已恢复显示：
- 创建：需要已发现 LAN 世界；EasyTier 启动后显示 `U/` 码；
- 「复制手机邀请（含 EasyTier / 基岩地址）」复制：
  ```
  pclpro://join?c=U/XXXX-XXXX-XXXX-XXXX&h=10.114.51.41&p=19132&n=scaffolding-mc-XXXX-XXXX&s=XXXX-XXXX
  ```
- 手机导入后按提示用 EasyTier 加入同一网络，再进 `10.114.51.41:19132`。
- EasyTier host 侧额外放行 `19132/19133`（基岩桥/NetherNet）TCP/UDP；自建中继 `tcp://124.221.231.213:11010` 已显式注入房间启动路径。

> 跨网络前提：中继可达。当前实测 `11010` 拒绝连接、公共节点域名在本机不可解析；若无法恢复中继，C 只保证同 Wi-Fi 场景。

## 2. 手机端（PCL Pro 手机版 1.0 联机助手，1.1.0）

> 手机源码：<https://github.com/yangtianyi117-maker/PCL-Mobile-Link>（APK 走 Releases；`dist/`、`keystore/` 不入库）。

### 2.1 官方服务器（A）
大厅顶部「官方服务器」卡片：
- 每进页面自动 RakNet ping `124.221.231.213:19132`，显示 `在线 0/12 · 45 ms`；
- 同时用 Java Ping 显示 `25566` 的 `26.3 0/12`；
- 「进入」：复制地址并尝试 `minecraft://connect?...`，失败回退打开 Minecraft，最后回退仅复制；
- 「复制手机地址」「复制 PC 地址」。

### 2.2 局域网（B）
原「扫描局域网」「手动添加」保持不变；PC 基岩桥启动后同一 Wi-Fi 可扫到。

### 2.3 房间码（C）
「房间」页：
- 输入 `U/XXXX-XXXX-XXXX-XXXX`；
- 「解析房间码」按 PCL CE 规则派生：
  - 网络名 = `scaffolding-mc-` + 码正文前 9 位（例如 `scaffolding-mc-R4YN-L7KP`）；
  - 密钥 = 码正文第 10 位起（例如 `A7Z2-JL9B`）；
- 「复制 EasyTier 配置」「打开 EasyTier」（检测 `com.easytier.app` 等，未安装跳官方 Releases）；
- 「进入基岩服」直连 `10.114.51.41:19132`；
- 支持 `pclpro://join?...` 深链导入并自动切到房间页。

### 2.4 构建
```powershell
cd 'C:\AndroidDev\pclpro-mobile'   # 桌面中文路径的 ASCII junction
powershell -ExecutionPolicy Bypass -File .\build.ps1
# 产物：dist\PCLProMobile-1.1.0.apk
```

## 3. Pojav（次级 APK，D）

- `Tools.PENDING_JOIN` + `--server/--port`：只消费一次，不影响后续正常启动；
- 「扩展中心 → 世界服务器」Java 行增加「进入」，设置目标后拉起 `LauncherActivity` 并自动点启动；
- 需要先在 Pojav 里安装对应 1.21.x 实例并选择账号；
- **26.3 不能进**：官方服要求 Java 25，Pojav 内置运行时是 17/21；JRE 25 导入/渲染未验证，不发布半成品。

## 4. 端口与地址

| 用途 | 地址 |
|---|---|
| 官方 Java | `124.221.231.213:25566` |
| 官方 Bedrock（Geyser） | `124.221.231.213:19132` |
| 同 Wi-Fi 基岩桥 | `0.0.0.0:19132`（RakNet）/ `19133`（NetherNet，冲突可配） |
| EasyTier 虚拟 IP | `10.114.51.41` |
| EasyTier 自建中继 | `tcp://124.221.231.213:11010` |
| 局域网应答器 | UDP 25566（`LinkLanPort`） |

## 5. 验证记录

| 项 | 证据 |
|---|---|
| 官方 Bedrock 在线 | `java PingTest 124.221.231.213 19132 3000` → `Geyser`、`0/12`、约 45 ms |
| 官方 Java 在线 | mcsrvstat `/3/124.221.231.213:25566` → `26.3`、`0/12` |
| 手机官方卡 | 模拟器截图 `artifacts/mobile-official-2.png`：`在线 · 0/12 人 · 58 ms`，PC `26.3 0/12` |
| 桌面手机卡 | `artifacts/pcl-phaseA.png`：`在线 · 0/12 人 · 41 ms`，Bedrock 26.63 |
| 手机房间码/深链 | `artifacts/mobile-room.png`：`U/R4YN...`、网络/密钥、`10.114.51.41:19132` |
| 桌面构建 | `dotnet build` 0 错误；`PCLPro.exe` payload `pclpro-2026-11-22.8` 已部署 |
| 手机构建 | `PCLProMobile-1.1.0.apk`、versionCode 2、同签名可覆盖升级 |
| 基岩桥核心 | PCL 自动启动桥 → Geyser `已在 UDP 端口 19132 上启动` → `PingTest 127.0.0.1 19132` 返回 `PCL Pro / 26.63 / 0/12 / 34ms` |
| Pojav 构建 | `PCL-Mobile-0.3.0.apk`（38.3 MB）已出包并安装；源码补丁含 `--server/--port` 与目录「进入」 |

未完成/待环境：
- B 的「手机真机同 Wi-Fi 进入」需要你拿手机验证（本机已用 RakNet 验证桥本身）；
- C 跨网依赖 EasyTier 中继恢复；
- D 的一键进服需要先有 1.21.x 实例/账号；26.3 仍受 JRE 25 限制。

## 6. 安全与边界

- 基岩桥只用于可信 Wi-Fi；服务启动时会监听 `0.0.0.0:19132`，不需要时请点停止；
- Geyser `auth-type: offline`，仅做协议翻译，不修改 Minecraft；
- 不内置 EasyTier APK（LGPL 合规），只检测/跳转官方应用；
- 不在日志打印完整房间密钥；
- 26.3 手机 Java 不在本次承诺内，避免发布不可用路径。
