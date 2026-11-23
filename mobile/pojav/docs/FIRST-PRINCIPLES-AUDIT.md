# 第一性审计 / First-Principles Audit

PCL 定制版 手机端 · 联机与互通方案 · 合规优先
PCL Custom Mobile · Multiplayer & Interop · Compliance First

日期 / Date: 2026-11-22

---

## 0. 结论摘要 / Executive Summary

| 需求 | 结论 | 合规做法 |
|---|---|---|
| 内置 Radmin LAN 安装包 | **不可行且不可分发** | Radmin VPN 是 Famatech 商业软件，无再分发授权；且仅支持 Windows，Android 无法运行。改为“检测/引导安装官方版本”或使用开源方案。 |
| 一键局域网联机 | **可行** | 同 WiFi 用 Minecraft 自带 LAN；跨网络用开源隧道（WireGuard/ZeroTier/n2n）或用户自装 Radmin。 |
| Java ↔ 基岩互通 | **可行（开源组件）** | Geyser（GPLv3）+ Floodgate（MIT）本地桥接；随包分发需履行 GPLv3 源码义务。 |
| 与网易版互通 | **仅官方授权下可行** | 不做协议逆向、不绕过鉴权；只接受网易官方 API/SDK/合作授权；否则不提供该功能。 |

---

## 1. 第一性：启动器能合法做什么

1. **不打包、不再分发任何闭源商业软件**（Radmin、网易客户端、Minecraft 本体）。
2. **可以复用**：OpenJDK（GPLv2+CE）、LWJGL（BSD-3）、GL4ES（MIT）、Geyser（GPLv3）、Floodgate（MIT）。
3. **可以调用**：用户本机已安装的软件（Radmin），通过系统 API 启动其界面，不复制其文件。
4. **必须提供**：GPL 组件的完整对应源码与修改说明（我们已发布 PCL-Mobile 仓库）。

## 2. Radmin LAN：为什么不能“内置”

- 文件：`Radmin_LAN_2.1.4952.2.exe`（55,371,080 B，SHA-256 `717A8714…D5A4`），
  版权方 **Famatech Corp.**，产品 **Radmin VPN**。
- 其许可禁止未经授权的复制与再分发；把它塞进我们的安装包/APK 属于侵权。
- 它是 **Windows x86/x64 安装程序**，Android 无法执行；手机端“内置”在技术上也不成立。

### 合规的“一键联机”设计

- **PC**：
  - 检测 `Radmin VPN` 是否已安装（注册表/开始菜单）；
  - 已安装 → 一键拉起 Radmin 并复制房间信息；
  - 未安装 → 打开 **官方下载页**（用户自行安装并接受其许可），不随包分发。
- **手机**：
  - 同 WiFi：直接使用 Minecraft 的 LAN 发现；
  - 跨网络：使用 **WireGuard**（GPLv2）/ **ZeroTier**（BSL 1.1）/ **n2n**（GPLv3）等开源方案，
    或用户自行在 PC 侧建 Radmin 房间、手机通过同一局域网接入；
  - 不在 APK 内分发任何闭源 VPN。

## 3. Java ↔ 基岩互通

架构（本地桥接，不依赖第三方服务器）：

```
Java 客户端 ──> Java 服务端（Paper/Spigot，GPL 系）
                     │
              Geyser（GPLv3） + Floodgate（MIT）
                     │
基岩客户端 ──> 同一局域网 / 同一隧道
```

- **Geyser**：把基岩协议翻译为 Java 协议，GPLv3；可随包分发，但必须提供源码与修改说明，
  并以 GPLv3 兼容方式链接。
- **Floodgate**：让基岩玩家免 Java 正版登录进入，MIT。
- 手机端可在 PCL 的 JVM 中启动一个**本地 Geyser 实例**（Java 进程），PC 端同理；
  这对手机性能有要求，建议作为可选组件按需下载。
- 不修改、不逆向 Minecraft 客户端/服务端协议实现；Geyser 是独立实现，合规。

## 4. 与网易版互通的初步计划（合规红线）

**红线：不逆向网易协议、不绕过登录/鉴权、不修改网易客户端、不分发其资源。**

可行路径（按优先级）：

1. **官方合作**：联系网易《我的世界》中国版商务/开放平台，申请服务器互通或数据导入的正式授权/SDK；
   拿到书面授权后再实现。
2. **通用格式导入（不涉及协议）**：仅支持用户自行导出的 **存档（NBT/区域文件）与资源包** 的格式转换，
   不读取网易客户端私有数据库、不破解加密资源；在 UI 中明确提示“需用户自行导出，遵守网易用户协议”。
3. **不做**：客户端协议桥接、登录态伪造、皮肤/资源解密。

## 5. 许可矩阵

| 组件 | 许可 | 可再分发 | 义务 |
|---|---|---|---|
| OpenJDK | GPLv2+CE | 是 | 源码 |
| LWJGL | BSD-3 | 是 | 保留版权声明 |
| GL4ES | MIT | 是 | 保留版权声明 |
| Geyser | GPLv3 | 是 | 源码 + 修改说明 |
| Floodgate | MIT | 是 | 保留版权声明 |
| Radmin VPN | 商业闭源 | **否** | 仅引导安装 |
| 网易我的世界 | 商业闭源 | **否** | 仅官方授权 |

## 6. 落地节奏

1. 发布中英文 PCL-Mobile v0.2.0（本仓库 Release）；
2. PC 端加“联机”页：检测/引导 Radmin + 同 WiFi LAN 说明；
3. 手机端加“同 WiFi 联机”入口与 Geyser 可选组件下载（GPLv3 源码同步发布）；
4. 网易互通仅保留“官方授权洽谈中”占位，不实现任何逆向功能。
