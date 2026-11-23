# 许可与合规

> 本文件是工程清单，不是法律意见。上线前请让懂软件/游戏许可的人复核。

## 1. 地图（最关键）

**EarthMC Full Earth (2026) 1:1000 Vanilla**，购买即获得 `https://earthmc.org/license` 授予的许可。要点：

- ✅ 允许：在**你拥有或控制**的 Minecraft 服务器上安装、修改、运行该地图；玩家通过正常游戏协议探索。
- ✅ 允许：合理的私有备份；截图/直播/视频。
- ❌ 禁止：再分发、上传、镜像、转售、租赁、打包、分享下载链接/账号/世界文件夹/备份。
- ❌ 禁止：把地图文件放进公开仓库、公开对象存储、共享网盘、可下载的服务器目录。
- ❌ 禁止：分发「包含该地图实质部分」的修改版/转换版。
- ⚠️ 公网 BlueMap/Dynmap 渲染是否属于「分发衍生版本」，许可文本没有明确；**默认只开内网**，并向 `sales@earthmc.net` 书面确认后再公开。

工程约束：

- 客户端 ZIP **不得**包含 `Earth1000/`、`region/`、`level.dat`、`.mca`；
- 服务器不得提供世界下载（包括 `/download`、网盘链接、对象存储直链）；
- 备份文件放在私有目录，权限 600；
- 玩家只能通过游戏协议探索，不能拿到世界文件。

## 2. PCL / PCL Pro

- PCL2 是「公开源码、非开源」的自定义许可；PCL CE 的 `PCL.Core/` 等目录使用 Apache-2.0，`Plain Craft Launcher 2/` 使用 PCL 自定义许可。
- 本项目**不修改、不重新打包、不再分发 `PCLPro.exe` / `PCLProRuntime`**；只写 `servers.json`、`PCL.ini`、`versions/*.json`、`servers.dat` 这些数据文件。
- 客户端包不包含 PCL 二进制，安装器要求用户已有 PCL Pro。
- 对外名称使用「PCL Earth（第三方地球服整合）」，不暗示官方背书。

## 3. 客户端 Mod

| Mod | 许可 | 我们的用法 |
|---|---|---|
| Xaero's Minimap / World Map | All Rights Reserved（Modrinth） | 安装器从 Modrinth 官方 API 下载到玩家本地，不随包分发；保留作者署名与链接 |
| Sodium / Lithium / FerriteCore / Fabric API / Mod Menu / Cloth Config | 各自开源许可（LGPL/MIT/Apache 等） | 同上，从 Modrinth 下载 |

Xaero 的 modpack 条款要求：不得让包名/描述与其 mod 混淆；如在外部分发需给出官方链接署名。我们在 `README` 与安装器输出中保留链接。

## 4. 服务端插件

| 组件 | 许可（以仓库为准） | 义务 |
|---|---|---|
| Paper | GPL-3.0（Patch 部分） | 不修改分发即可 |
| Geyser | MIT | 保留版权声明 |
| Floodgate | MIT | 保留版权声明 |
| Towny | 见仓库 LICENSE | 保留声明 |
| BlueMap | 见仓库 LICENSE | 保留声明 |
| Chunky / ChunkyBorder | 见仓库 LICENSE | 保留声明 |
| CoreProtect | 见仓库 LICENSE | 保留声明 |
| EssentialsX / Vault | 见仓库 LICENSE | 保留声明 |
| LuckPerms | 见仓库 LICENSE | 保留声明 |
| AuthMe | 见仓库 LICENSE | 保留声明 |
| ViaVersion / ViaBackwards | 见仓库 LICENSE | 保留声明 |

部署时 `server/plugins.lock.json` 记录每个插件的版本、来源与 SHA256；`server/plugins/NOTICE/` 保存对应 LICENSE 文件。

## 5. Minecraft / Mojang

- 不分发游戏本体、资源、账号；
- 不销售影响游戏平衡的内容；
- 服务器遵守 Minecraft EULA 与 Commercial Usage Guidelines；
- 如接受赞助，仅限外观/排队/社区权益，不做 pay-to-win。

## 6. 数据与隐私

- AuthMe 只存必要的账号信息；不收集与游戏无关的个人数据。
- 日志保留 30 天；崩溃报告仅用于排障。
- 玩家可申请删除账号数据。

## 7. 合规检查清单（上线前）

- [ ] 客户端 ZIP 扫描：无 `region/`、`level.dat`、`.mca`
- [ ] 服务器目录不可 HTTP 下载（`/srv/pcl-earth` 不在 web root）
- [ ] 备份目录权限 600，且不在公开桶
- [ ] `LICENSE-NOTICES.txt` 随客户端包分发
- [ ] 公网 BlueMap 已获书面许可，或保持关闭
- [ ] 赞助页面不含数值/领地/战斗力
