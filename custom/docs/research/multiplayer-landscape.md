# 全网「Minecraft 联机房」搜集与机制检索

> 日期：2026-11-21　范围：Java 版 + 基岩版；公共服务器目录、房间码型联机、组网/穿透、平台大厅
> 目的：为 PCL Pro 的「服务器列表」提供候选数据，并判断「房主开房」这条路要不要自研。
> 标注：✅ = 本次实测过；📚 = 仅从目录/文档收录，未逐一 ping。

## 0. 结论先行

1. **公共服务器目录**是现成、可爬、可验证的数据源：`play.mcmod.cn`（中文，实时在线人数）、
   `minecrafttools.wiki`、`find-minecraft-servers.net`、`serverbuddy.net`、`minecraft.buzz`（基岩）。
2. **房间码型联机**已经有成熟方案，且和我们踩过的坑完全重叠：
   **Terracotta（陶瓦）**基于 EasyTier、由 PCL-Community 维护 PCL 分支、AGPL 允许「打包未修改二进制」；
   **Campfire** 做邀请码 + 大厅 + 房间设置；**e4mc / World Host / Essential** 走「单人世界 → 临时域名/IP」；
   **playit.gg** 是通用隧道。
3. 因此「房主电脑开房」**不该自研**：要恢复就接 Terracotta（或 e4mc 的临时域名思路），
   我们的 EasyTier 经验、房间码、签名房间记录都能对上。
4. 基岩版有一个新坑：1.26.51 起 Bedrock 走 **NetherNet**，单条 UDP 隧道不够，
   需要 TCP 信令 + WebRTC/UDP（Geyser 的 `transport: both` + `19132/TCP` + `19133/UDP` 正是这个原因）。

## 1. 公共服务器目录（可直接喂给启动器）

| 目录 | 语言/平台 | 数据形态 | 备注 |
|---|---|---|---|
| [play.mcmod.cn 找服玩](https://play.mcmod.cn/list/) | 中文 · Java | 服务器名 + 在线人数（实时） | 本次抓到单分类 **4694 人在线**，另一分类 7534 |
| [minecrafttools.wiki/servers](https://minecrafttools.wiki/servers) | 英文 · Java | 在线数、版本、国家、标签 | 有 Hypixel 12k+ 记录 |
| [find-minecraft-servers.net](https://find-minecraft-servers.net/) | 英文 · Java/Cross-play | 在线/上限、平台标记 | 含 ArchMC、SaltyMC |
| [serverbuddy.net](https://serverbuddy.net/minecraft-servers/most-popular) | 英文 · Java | 24h 峰值 | Hypixel 23k 峰值 |
| [minecraft.buzz/bedrock](https://minecraft.buzz/bedrock) | 英文 · 基岩 | 版本过滤 | 含 MellowCraft、Jartex、KiwiSMP |
| [planetminecraft 服务器区](https://www.planetminecraft.com/servers/) | 英文 · Java | 社区投票 | 适合「小型私服」 |

### 实测样本（本次用 mcsrvstat v3 直连查询）

| 服务器 | 地址 | 实测 |
|---|---|---|
| Hypixel | `mc.hypixel.net` | ✅ 在线 **20,579 / 200,000**，支持 1.8–26.3 |
| CubeCraft | `play.cubecraft.net` | ✅ 在线 **724 / 5,000**，1.20–1.21（Java 与基岩同名地址） |

> 查询方式：`https://api.mcsrvstat.us/3/<host>`；基岩用 `https://api.mcsrvstat.us/bedrock/3/<host>`。
> 实测证明：**启动器完全可以在拉目录后自行 ping 出「人数/延迟」，不需要服务器方配合**。

## 2. 房间 / 邀请码型联机（房主电脑开房）

| 方案 | 原理 | 房间码 | 需要房主公网？ | 基岩 | 成本 | 结论 |
|---|---|---|---|---|---|---|
| **Terracotta（陶瓦）** | 基于 EasyTier 的 P2P 组网 + 房间码，Rust 库，支持 HMCL / PCL.Proto | ✅ 房间码 | 否（可借公共节点/自建） | ❌ Java | 开源 AGPL（允许打包未修改二进制） | **最贴近我们现状**：EasyTier 底子、房间码、启动器集成 |
| **Campfire** | 进单人世界自动「对局域网开放」+ 邀请码 + 联机大厅/白名单/语音 | ✅ 邀请码 | 否 | ❌ | 开源工具箱 | 功能最像「联机房」：大厅、公开房间、房间设置 |
| **e4mc** | 把「对局域网开放」映射到临时公网域名（中继） | 临时域名 | 否 | ❌ | 免费中继 | 最省事、无大厅 |
| **World Host** | 模组，`/worldhost ip` 拿隧道 IP，带好友系统 | IP/临时 IP | 否 | ❌（计划 Geyser） | 免费 | 与 e4mc 同类 |
| **Essential Mod** | 社交/好友系统内建房 | 好友邀请 | 否 | ❌ | 免费+付费 | 社区评价一般 |
| **playit.gg** | 通用 TCP/UDP 隧道 | 分配域名 | 否 | ⚠️ 1.26.51 后 NetherNet 需额外端口 | 免费/付费 | 通用但配置繁琐 |

## 3. 组网与穿透（自己搭）

| 方案 | 原理 | 延迟 | 上手 | 备注 |
|---|---|---|---|---|
| **IPv6 直连** | 双方有公网 v6 直接连 | 最低 | 简单 | 国内家宽 v6 普及率高，但酒店/校园网易断 |
| **frp / nps** | 自有公网服务器反代 | 中 | 中 | 需公网服务器；frp 有 UDP 支持 |
| **SakuraFrp / 花生壳** | 商业内网穿透 | 中 | 简单 | 免费额度有限、限速 |
| **ZeroTier** | 虚拟二层网络 | 中低 | 中 | 需自建 moon 才快 |
| **Tailscale** | WireGuard 组网 | 低 | 简单 | 海外协调服务器 |
| **EasyTier** | P2P + 中继，Rust | 低 | 中 | **我们之前用的就是它**；公共节点收紧后需自建 |
| **Radmin VPN / Hamachi** | 传统虚拟局域网 | 高 | 简单 | 老牌、体验一般 |

## 4. 平台大厅

| 平台 | 形态 | 备注 |
|---|---|---|
| 网易《我的世界》 | 官方大厅 + 房间号 | 仅网易版客户端 |
| 联机盒子类 App | 房间号 + 地图 + 密码 | 移动端为主，第三方 |
| HMCL / PCL.Proto | Terracotta 联机版块 | 房间码、启动器内完成 |
| PCL Pro（我们） | 固定服务器列表 | 本轮已落地：`servers.json` + 实时 ping |

## 5. 对 PCL Pro 的判断

1. **固定服务器列表**保持现状：数据驱动 `servers.json`，加一条 = 改一个 JSON；候选见
   `docs/research/servers-candidates.json`。
2. **房主开房**若要恢复：直接接 **Terracotta**（PCL-Community 有分支、AGPL 例外允许打包二进制），
   不要重写 EasyTier/房间码/大厅；我们已有的房间码归一化、ECDSA 房间记录可以给「大厅」用。
3. **基岩入口**继续用服务器端 Geyser（已跑），手机侧零客户端；1.26.51+ 的 NetherNet 要求
   我们保持 `19132/TCP`（信令）+ `19132/UDP`（RakNet）+ `19133/UDP`（WebRTC）三条通路都放行。

## 6. 未完成 / 风险

- 中文服（mcmod 列表里 50+ 个）**没有逐一 ping**：多数只给名称与在线数，地址需要进详情页抓；
  可作为下一轮抓取目标（列表页 + 详情页 URL 规则）。
- 海外大服对国内延迟高，加入国内玩家的 `servers.json` 要标注「海外线路」。
- 目录站的反爬与字段差异，需要写一个抓取器（mcmod 列表页 → 详情页 → 地址/版本/端口）。

## 7. 第二轮：自动化抓取 + 实测（40 台）

**管线**：`tools/research/collect.mjs`
目录页 → 正则抽 host → Java 状态 ping / Bedrock RakNet ping（均自实现，无第三方 API）→
`docs/research/servers-collected.json`（含 `verified{online,max,latency,version}`）。

| 阶段 | 数字 |
|---|---|
| 从 [minecraft.buzz/bedrock](https://minecraft.buzz/bedrock) 抽到 | 37 个候选 |
| 从 [find-minecraft-servers.net](https://find-minecraft-servers.net/) 抽到 | 47 个候选 |
| 去重后实际探测 | **40 台** |
| 实测在线 | **27 台**（Java 10 / 基岩 17） |
| 在线玩家合计 | **27,313 / 110,433** |

**实测 Top 10（2026-11-21 09:2x）**

| 服务器 | 在线 | 延迟 | 来源 |
|---|---|---|---|
| buzz.manacube.com:19132 | 3,040/3,500 | 607 ms | minecraft.buzz |
| play.talonmc.net:19132 | 2,601/5,000 | 607 ms | minecraft.buzz |
| hub.manacube.com:25565 | 2,510/3,500 | 881 ms | find-minecraft-servers |
| play.enchantedmc.net:25565 | 1,989/2,026 | 988 ms | find-minecraft-servers |
| buzz.enchantedmc.net:19132 | 1,989/2,025 | 736 ms | minecraft.buzz |
| play.fadecloud.com:25565 | 1,824/10,000 | 1,365 ms | find-minecraft-servers |
| buzz.bagelsmp.com:19132 | 1,379/35,000 | 393 ms | minecraft.buzz |
| play.mc-complex.com:25565 | 1,346/3,000 | 943 ms | find-minecraft-servers |
| play.manacube.com:25565 | 1,255/3,500 | 882 ms | find-minecraft-servers |
| bedrock.vortexnetwork.net:19132 | 1,087/3,000 | 369 ms | find-minecraft-servers |

**mcmod 找服玩**（中文最大）：抓到其内部接口 `POST https://play.mcmod.cn/frame/serverList/`，
body `data={"classid":352,"page":N}`，返回 JSON（`{state,html}`），单分类 **605 台服务器 / 26 页**；
但**匿名用户拿不到服务器地址**（详情页只给版本、在线人数、类型、线路、白名单、正版验证、QQ 群），
所以它适合做「中文服元数据源」，地址需要登录态或另找来源。

**结论**：地址型目录（buzz / find-minecraft-servers）能直接喂 `servers.json`；
中文服目录能提供名称/在线数但缺地址，二者互补。
## 8. 第三轮：扩源到 140 台，并打通中文服地址

新增数据源（都在 `tools/research/collect.mjs` 的 `sources` 里）：

| 来源 | 类型 | 本页抽到 | 备注 |
|---|---|---|---|
| [mclists.cn](https://www.mclists.cn/index.html) | 中文 · Java | 63 | **带 IP 端口**，中文服首选源 |
| [topg.org](https://topg.org/minecraft-servers) | 英文 · Java | 23 | 老牌榜单 |
| [minecraftservers.org](https://minecraftservers.org/) | 英文 · Java | 8 | 首页少量 |
| minecraft.buzz / find-minecraft-servers | 英文 · 基岩+Java | 46 | 第二轮已有 |

**本轮成绩：140 台收录 / 75 台在线 / 约 3.6 万在线玩家**

| 来源 | 收录 | 在线 | 在线玩家合计 |
|---|---|---|---|
| find-minecraft-servers.net | 20 | 15 | 15,274 |
| minecraft.buzz | 26 | 15 | 13,086 |
| minecraftservers.org | 8 | 4 | 4,160 |
| topg.org | 23 | 19 | 3,159 |
| **mclists.cn（中文）** | **63** | **22** | **823** |

**中文服 Top（实测，已写入 `servers-cn.json`）**

| 服务器 | 在线 | 延迟 | 版本 |
|---|---|---|---|
| mcbi.top:25565 | 445/1,099 | 283 ms | XCord 1.7.x–1.21.x |
| mc.zenoxs.cn:25565 | 123/2,026 | 513 ms | Velocity 1.7.2–26.3 |
| mc101.ytonidc.com:50305 | 51/100 | 1,964 ms | Paper 26.1.2 |
| mc.meows.cc:30626 | 48/150 | 1,220 ms | 26.3 |
| a2.sjcmc.cn:10448 | 22/300 | 2,050 ms | 1.12.2 |
| mc78.starmc.cn:25565 | 19/1,001 | 550 ms | 群组 |
| xkhjmc.cc:25565 | 17/100 | 615 ms | Velocity 1.7.2–26.3 |

> 注意：国内服延迟普遍高于海外大服（283–2,900 ms），说明这些服多为海外机房或线路绕行；
> 入库前应按「实测延迟」筛，而不是按「.cn 域名」筛。

**工程改进**：给探测加了 `hardTimeout` 与逐条 try/catch——
之前某个域名 DNS 挂起会让整个 `Promise.all` 悬空、Node 直接以 unsettled await 退出；
现在单台最坏 14 s（两次 7 s），批量不会再被一台拖死。
## 9. 第四轮：mclists 结构化 API，一次收录 3,580 台中文服

**关键发现**：mclists.cn 有公开 JSON API，不需要抓 HTML。

```
GET https://www.mclists.cn/api/v2/servers?page=N&limit=50
→ { data: [...], meta: { page, limit, total: 3591, total_pages, sort: "recommended" }, links }
```

单条字段：`sid / name / description / address / port / endpoint / edition / category / status /
players{online,maximum} / version / country_code / updated_at / url / recommendation`。

**本轮成绩**

| 项 | 数值 |
|---|---|
| 抓取页数 | 72 页 × 50 |
| 去重后 | **3,580 台**（API 标称 3,591） |
| 快照在线 | 597 台（其中 344 台有玩家） |
| 在线玩家合计 | 12,437 |
| 基岩版条目 | **仅 2 台**（中文基岩是明显空白） |

**分类分布**（tags 里的分类）：生存 1,518、RPG 1,106、模组 281、宝可梦 211、群组 149、工业 121。

**版本分布 Top**：1.12.2（167）、Paper 1.21.11（127）、1.20.1（96）、Velocity 1.7.2–26.3（80）、
Paper 26.2（79）、Velocity 1.7.2–26.2（70）。

**合并总库**：`docs/research/servers-all.json` = **3,697 台**去重（mclists 3,601 + 海外四源 96）。

**脚本**：`tools/research/collect-mclists.mjs`（参数：页数、每页条数），纯 fetch，无依赖。

**下一步**：中文基岩服是空白（只有 2 台），需要换源（基岩服务器列表/QQ 群/贴吧）；
另外 3,580 台里只有 597 台快照在线，说明「收录量」≠「可用量」，入库前要按 ping 复核。
### 9.1 抽样复核（快照 vs 实时）

对 `servers-all.json` 里快照人数最高的 40 台做实时 Java ping：

| 指标 | 结果 |
|---|---|
| 抽样 | 40 台 |
| 实时可连 | **37 台（92.5%）** |
| 人数一致性 | 基本吻合（误差多在 ±5% 内） |

样例：

| 服务器 | 快照人数 | 实时人数 | 延迟 |
|---|---|---|---|
| hub.opblocks.com:25565 | 3,031 | 3,018/3,500 | 844 ms |
| hub.manacube.com:25565 | 2,510 | 2,504/3,500 | 781 ms |
| play.enchantedmc.net:25565 | 2,017 | 1,953/2,026 | 850 ms |
| 2026.lsfun.fun:32308 | 1,286 | 1,830/2,026 | 711 ms |
| play.cubecraft.net:25565 | 722 | 734/5,000 | 2,617 ms |

结论：**mclists 的小时级快照可以直接当「目录数据」用**，上线前只需对将要展示的那一页做实时复核。
复核脚本：`tools/research/verify-sample.mjs`。
## 10. 第五轮：beaconservers 的 markdown 端点，总库破 1.3 万台

**发现**：`beaconservers.com` 提供按页的 markdown 端点，字段规整、无广告干扰：

```
GET https://beaconservers.com/minecraft-servers/{java|bedrock}.md?page=N
→ Markdown 列表；每块含 Server address / Edition / Status / Players / Version / 描述
```

| 分区 | 标称总量 | 页数 | 本轮抓取 | 去重后 |
|---|---|---|---|---|
| java | **19,643** | 410 | 200 页 | **9,058** |
| bedrock | 1,106 | 24 | 24 页 | 971（其中基岩 302） |

**基岩版实时复核**（RakNet ping，302 个基岩入口）：

| 指标 | 结果 |
|---|---|
| 实时可连 | **233 / 302 = 77.2%** |
| 快照 vs 实时人数 | 基本一致（偏差 ±3% 内） |

基岩 Top（实测）：

| 服务器 | 在线 | 延迟 |
|---|---|---|
| play.lbsg.net:19132 | 6,091/16,091 | 1,413 ms |
| play.inpvp.net:19132 | 2,947/10,000 | 307 ms |
| play.megasmp.gg:19132 | 2,557/10,000 | 351 ms |
| zeqa.net:19132 | 1,719/1,729 | 414 ms |
| ru.cristalix.pe:19132 | 1,304/10,000 | 899 ms |
| play.easecation.net:19132 | 1,311/21,922 | 2,023 ms |

**合并总库（servers-all.json）**

| 维度 | 数值 |
|---|---|
| 去重服务器 | **13,251** |
| Java / 基岩 | 12,918 / **333** |
| 各目录快照在线人数合计 | 292,905 |
| 来源 | beaconservers 9,554 · mclists 3,601 · 海外四源 96 |

**脚本**：`tools/research/collect-beacon.mjs`（分区/页数可调）、`tools/research/verify-bedrock.mjs`（RakNet 复核）。

**仍缺**：beaconservers java 还有 210 页没抓（约 1 万台）；中文基岩服依旧稀缺（mclists 仅 2 台，beacon 基岩以海外为主）。
## 11. 第六轮：抓完 beaconservers 全部 410 页，总库 20,738 台；开始按可用性分档

**续抓**：beaconservers java 201–410 页 → 再去重 9,569 台（多为 0 人小服）。
至此 beaconservers java 的 **410/410 页全部抓完**。

| 累加 | 数量 |
|---|---|
| 上一轮总库 | 13,251 |
| + java2（201–410 页） | **20,738**（20,404 Java / 334 基岩） |
| 快照在线人数合计 | 292,906 |

**可用性分档**（从快照人数 Top 1000 里实测 Java）：

| 项 | 结果 |
|---|---|
| 实测样本 | 1,000 台 |
| 实时可连 | **333 台（33.3%）** |
| 延迟档 A（<300 ms） | 9 台 |
| 延迟档 B（300–800 ms） | 26 台 |
| 延迟档 C（≥800 ms） | 298 台 |

**最快的一批（A 档）**：`101.245.164.244:30000` **150 ms / 534 人**、`cnvpvp.top:25565` 209 ms / 412 人、
`180.188.16.69:25565` 201 ms / 390 人。

**最大的**：`donutsmp.net` **18,540/50,000**（574 ms）。

**结论（很重要）**：目录里「在线」的 1,000 台里只有 **33%** 现在真的能连——
之前 Top40 抽样 92.5% 是因为头部大服稳定；越往尾部，快照越不可靠。
所以「展示列表」必须**实时探测后再渲染**，不能直接照搬目录。

产出：
- `servers-curated.json`：1,000 台实测 + A/B/C 分档
- `servers-candidate-top.json`：实测在线 Top30，字段与 `servers.json` 一致
- `tools/research/curate.mjs`：探测 + 分档脚本