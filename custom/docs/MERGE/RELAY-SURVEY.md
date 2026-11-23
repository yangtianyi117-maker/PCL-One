# 中继服务器现状勘察（124.221.231.213）

> 日期：2026-11-21　方式：SSH（`ubuntu`，密码登录；root 不允许）
> 结论：**中继已经存在**，缺的只是「动态端口 + 房间注册」这一段；公网新端口需要云控制台放行。

## 1. 机器

| 项 | 值 |
|---|---|
| 主机名 | `VM-0-6-ubuntu`（Ubuntu 26.04 系内核 6.8.0-124） |
| CPU / 内存 | 2 vCPU / 3.7 GB（已用 3.06 GB，剩余约 **666 MB**） |
| 磁盘 | 59 GB，已用 11 GB（18%） |
| 负载 | 1.07 / 1.03 / 1.00（2 核，接近满载） |
| sudo | `ubuntu` 免密 sudo |

## 2. 已经在跑的服务（真正的中继三件套）

| 服务 | 端口 | 作用 |
|---|---|---|
| `frps.service` | TCP 7000 | frp 服务端（已有 token 鉴权），可做 TCP/UDP 端口转发 |
| `easytier-relay.service` | UDP/TCP 11010、15888-15890、11021/11022 | EasyTier 公共中继（名 `mymc`） |
| `geyser.service` | UDP **19132**、19133 | Geyser Standalone（`-Xmx384M`），基岩 19132 → Java 25566 |
| `vanilla.service` | TCP 25566 | MC 原版 26.3 生存服 |
| `titan107.service` | TCP 25565 | MC 1.7.10 Forge + TheTitans + Techguns |
| `titan-kit.service` | — | 1.7.10 进服发装备（RCON） |
| `mcp-api.service` | TCP 8088 | MetaYuan Platform API（python3） |
| `nginx.service` | TCP 80 | 静态站，`/pclpro/` 是启动器更新通道 |

内存占用前三：`java 1.07 GB`（titan）、`java 1.03 GB`（vanilla）、`java 454 MB`（geyser）。

## 3. 实测

| 测试 | 结果 |
|---|---|
| 本机 → `124.221.231.213:19132` RakNet ping | **通**：`0x1C`，MOTD `MCPE;"原版生存 | Vanilla 26.3";2225;26.63;…;Geyser;Survival` |
| 机内 `127.0.0.1:21000` → `127.0.0.1:19132` UDP 转发 | **通**：pong 与直连完全一致（临时脚本，已停） |
| 本机 → `124.221.231.213:21000` | **不通**（超时） |
| 本机防火墙 | ufw 未启用；iptables 策略 ACCEPT，`YJ-FIREWALL-INPUT` 只封了个别 IP 的 22 |

→ 结论：**阻断在腾讯云安全组**，机器内部没有拦截。新端口需要在云控制台放行。

## 4. 对「手机互通」的意义

1. 对你自己的服务器，中继已经完成：手机在**任意网络**直接连 `124.221.231.213:19132` 就能进原版 26.3。
2. 对 PCL Pro 的房间，只需补两件事：
   - 中继上一个「端口池 + UDP 转发」守护（21000-21099，每房 1 个端口、约 0 内存）；
   - 一个房间注册 API（建房时登记房主的 EasyTier 虚拟 IP + 19132，返回公网端口）。
   房主**不需要**装 frpc：EasyTier 已经在跑，中继能直接路由到房主虚拟 IP。
3. 容量红线：只剩 666 MB，**不能**每房起一个 Geyser（每实例 300-450 MB）；只能做转发，Geyser 留在房主本机（已随 PCL Pro 融合，`PCL\Geyser\Geyser-Standalone.jar`）。

## 5. 待办 / 待确认

- [ ] 云控制台放行 **UDP 21000-21099**（或指定一个可用的端口段）。
- [ ] 公网带宽规格（决定同时几个手机玩家；基岩每人 0.2-0.6 Mbps）。
- [ ] 是否安装 SSH 公钥并关闭密码登录（当前密码较弱、公网可爆破）。
- [ ] 房间注册 API 放 8088（并入 mcp-api）还是新起 8089。

## 6. 清理结果（2026-11-21 执行）

| 项 | 前 | 后 |
|---|---|---|
| 可用内存 | 666 MB | 1694 MB |
| 磁盘占用 | 11 GB | 9.5 GB |
| 泰坦 4 个单元 + 数据 + cron | 运行中 | 已删（world/config 归档在 `/home/ubuntu/_archive`，7 天） |
| frps | 运行中（0 客户端） | 已删 |
| 25565 | 监听（泰坦） | 已关闭 |

保留：`vanilla`(25566)、`geyser`(19132→25566)、`easytier-relay`、`mcp-api`+`mcp-data`、`nginx`。
最终方案见 [SERVER-FIRST.md](SERVER-FIRST.md)。