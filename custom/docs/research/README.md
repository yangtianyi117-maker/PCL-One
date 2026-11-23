# 联机服务器搜集（收尾版）

> 目标：尽可能搜集和检索全网的 Minecraft 联机服务器/联机机制。
> 截止：2026-11-21（第 7 轮收尾）　最终总库：**20,738 台**（Java 20,404 / 基岩 334）

## 1. 成果一览

| 项 | 数值 |
|---|---|
| 去重服务器总数 | **20,738** |
| Java / 基岩 | 20,404 / 334 |
| 目录快照在线玩家合计 | 292,906 |
| 实测样本（快照 Top1000） | 可连 **333 台（33.3%）** |
| 延迟分档 | A(<300ms) 9 · B(300–800ms) 26 · C(≥800ms) 298 |
| 实测在线 Top30 | 已生成，字段与 `servers.json` 一致 |

### 数据源

| 来源 | 形态 | 收录 |
|---|---|---|
| [beaconservers.com](https://beaconservers.com/minecraft-servers/java.md) | Markdown 分页端点（410 页 Java / 24 页基岩） | 17,041 |
| [mclists.cn](https://www.mclists.cn/api/v2/servers) | 公开 JSON API（72 页 × 50） | 3,601 |
| minecraft.buzz / find-minecraft-servers / topg / minecraftservers | HTML 首页 | 96 |

### 联机机制检索（结论）

- **房间码型**：Terracotta（陶瓦，EasyTier 内核，PCL-Community 维护）、Campfire、e4mc、World Host、Essential。
- **穿透/组网**：IPv6 直连、frp/nps、SakuraFrp/花生壳、ZeroTier/Tailscale/EasyTier、Radmin/Hamachi。
- **平台大厅**：网易我的世界、联机盒子、HMCL/PCL.Proto。
- **结论**：房主电脑开房不应自研，接 Terracotta 或 e4mc 即可；基岩版 1.26.51+ 走 NetherNet，需要 `19132/TCP + 19132/UDP + 19133/UDP` 三条通路。

## 2. 文件

| 文件 | 用途 |
|---|---|
| `servers-all.json` | 总库（20,738 台，23.9 MB） |
| `servers-curated.json` | 1,000 台实测 + A/B/C 分档 |
| `servers-candidate-top.json` | 实测在线 Top30，可直接导入 `servers.json` |
| `servers-cn.json` | 中文服 Top20（带 IP） |
| `multiplayer-landscape.md` | 全部 7 轮的检索记录、来源、结论 |
| `servers-mclists.json` / `servers-beacon-*.json` / `servers-collected.json` | 各源原始数据 |

## 3. 脚本（无第三方依赖）

| 脚本 | 用法 |
|---|---|
| `collect.mjs` | 抓多个 HTML 目录并自研 Java/Bedrock 协议探测 |
| `collect-mclists.mjs` | `node ... 72 50`（页数 / 每页） |
| `collect-beacon.mjs` | `node ... java 210 out.json 201`（分区 / 页数 / 输出 / 起始页） |
| `curate.mjs` | `node ... 1000` → 实测 + 分档 |
| `verify-sample.mjs` / `verify-bedrock.mjs` | 抽样复核 |

## 4. 已知缺口

1. **中文基岩服稀缺**：334 台基岩里中文只占个位数；中文源（mclists）只收录 2 台基岩。
2. **目录漂移**：快照 Top1000 只有 33% 实时可连，展示前必须实时 ping。
3. 未覆盖的目录：minecraft-mp、mc.game、gamemonitoring.cn（本轮请求超时/被墙）。

## 5. 上线建议

- 用 `servers-candidate-top.json` 作为首版列表，**展示时用客户端 `McPing` 实时刷新人数/延迟**。
- 每天跑一次 `curate.mjs`，只保留可连的条目，避免列表腐化。
- 中文基岩作为下一阶段专项：换源 + 与 Geyser 公共入口结合。
