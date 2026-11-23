# 客户端 Mod 库 + 世界服务器（PCL Mobile 扩展）

> 日期：2026-11-22　　目标：在 `PCL Pro 手机版` 里集成“全网客户端 Mod”目录，并扩充公共服务器列表。
> 本文只记录本轮实际落地的内容、数据格式和验证证据，方便并行的其它会话接手。

## 1. 第一性：为什么是“客户端 Mod + 地址列表”

前几轮已经确认过边界：

- **客户端 Mod 只改变“你怎么看/怎么发”**，服务端不需要安装。所以一个 Mod 目录天然可以服务任何服务器。
- 例外是标注 **Both** 的条目（Figura、Emotecraft）：要别人也看见，必须对方装同一 Mod，服务端通常还要转发。
- **服务器列表是纯数据**：地址 + 版本 + 说明，不参与协议，不改变任何判定。

因此这次没有去碰协议、皮肤或反作弊，只在“本地投影层”和“数据层”做扩展——这是唯一不会把联机搞坏的位置。

## 2. 交付内容

### 2.1 客户端 Mod 目录

- 位置：`mobile-fork/app_pojavlauncher/src/main/assets/pcl_client_mods.json`
- 规模：8 个分类、48 个 Mod，全部指向 Modrinth 项目 slug。
- 分类：性能优化 / 画面与光影 / 界面与操作 / 信息与配方 / 地图 / 动画与模型 / 声音 / 前置库。
- 每个条目：`name`、`slug`、`side`（client / both）、`desc`、可选 `android` 警告。
- 安装：点“安装”后，App 读取当前实例的 `lastVersionId`，解析出 **MC 版本 + 加载器**，向 Modrinth 查询匹配构建，下载到该实例的 `mods/`。
  - 支持的版本 ID：`fabric-loader-…`、`quilt-loader-…`、`<mc>-forge-…`、`<mc>-neoforge-…`。
  - 原版实例会提示“请先安装 Fabric / Forge”。
  - 找不到匹配构建会明确提示 `没有适配当前实例（版本 / 加载器）的版本`。
- **全网搜索**：搜索框输入 ≥2 个字符后，350 ms 防抖，直接查 Modrinth `/v2/search`（20 条），
  按当前实例的 MC 版本 + 加载器过滤，并用 `client_side:required OR optional` 排除纯服务端 Mod；
  搜索结果同样可一键安装。搜索失败时回退到本地精选。
- **自动前置依赖**：安装时读取 Modrinth 版本的 `dependencies`，对所有 `required` 项递归安装；
  有 `version_id` 就按指定版本取，否则按项目 + 当前实例找最新匹配；按 project id 去重，已存在的 jar 不重复下载。

### 2.2 世界服务器列表

- 位置：`mobile-fork/app_pojavlauncher/src/main/assets/pcl_servers.json`
- 规模：27 个公共服务器（17 个 Java + 10 个 Bedrock）。
- 每个条目：`name`、`address`、`edition`、`version`、`region`、`desc`。
- 交互：一键复制地址；Bedrock 条目自带端口。
- **在线状态**：Java 版走 Server List Ping（1.7+ 状态协议），Bedrock 版走 RakNet 未连接 Ping（`0x01`→`0x1C`）；
  显示 `在线 人数/上限 · 延迟` 或 `离线 / 无响应`，2.5 秒超时，结果缓存，顶部有“刷新在线状态”。
- 页脚明确提示：公开地址会变动，进服前以官网/列表页为准。
- **自定义服务器**：顶部“添加服务器”可填名称/地址/Java 或 Bedrock，保存到 SharedPreferences；
  长按自定义行可删除；重启 App 后仍在，和精选列表一起参与在线探测与搜索。
- **Java 一键进服（2026-11-22）**：Java 服务器行新增「进入」：设置一次性 `Tools.PENDING_JOIN`，
  拉起 `LauncherActivity` 并自动启动当前实例，启动参数追加 `--server/--port`。
  需要先安装对应 1.21.x 实例并选择账号；Bedrock 行仍只复制地址。

### 2.3 UI

- 新 Activity：`CatalogActivity`（`net.kdt.pojavlaunch.catalog`），入口在主菜单 **EXTENSIONS / 扩展中心**。
- 三个标签页：**客户端 Mod**、**世界服务器**、**已安装**；支持按名称/描述/地址/文件名搜索。
- **已安装**直接扫描当前实例的 `mods/*.jar`，显示文件名与大小，可删除（带确认），顶部可刷新。
  对每个 jar 计算 SHA1 并查 Modrinth：能识别来源的显示当前版本；有更新时显示 `v旧 → v新` 和“更新”按钮。
- 顶部显示 **当前实例：<MC 版本> · <加载器>**；全网搜索按它过滤，避免推给你装不上的构建。
- 深色主题，纯代码绘制行，无新增图片依赖。

## 3. 代码结构

```text
app_pojavlauncher/src/main/
├─ assets/
│  ├─ pcl_client_mods.json        # 48 个客户端 mod 目录
│  └─ pcl_servers.json            # 27 个公共服务器
├─ java/net/kdt/pojavlaunch/catalog/
│  ├─ CatalogActivity.java        # 列表 / 搜索 / 标签页 / 复制地址
│  └─ ClientModInstaller.java     # 实例版本解析 + Modrinth 查询 + 下载到 mods/
├─ res/layout/activity_catalog.xml
├─ res/values/strings_catalog.xml
└─ res/values-zh-rCN/strings_catalog.xml
```

改动的既有文件（都很小，避免碰并行会话正在改的 `Tools.java`）：

- `AndroidManifest.xml`：注册 `CatalogActivity`
- `res/layout/fragment_launcher.xml`：主菜单加 `catalog_button`
- `MainMenuFragment.java`：按钮跳转

## 4. 数据格式（扩展方式）

### 4.1 加一个客户端 Mod

在 `pcl_client_mods.json` 对应分类的 `mods` 数组追加：

```json
{ "name": "Mod 名称", "slug": "modrinth-slug", "side": "client", "desc": "一句话说明" }
```

- `slug` 必须能在 `https://modrinth.com/mod/<slug>` 打开。
- 双端可见的写 `"side": "both"`。
- Android 上不兼容的渲染类 Mod 可加 `"android": "Android 上可能不兼容"`。

### 4.2 加一个服务器

在 `pcl_servers.json` 的 `servers` 数组追加：

```json
{ "name": "名称", "address": "地址:端口", "edition": "java", "version": "1.21", "region": "全球", "desc": "说明" }
```

- Java 版不带端口；Bedrock 版必须带 `:19132` 之类的端口。
- 地址会过期，建议只收录有官网可查的服务器。

## 5. 构建与验证

### 5.1 构建

```powershell
$env:JAVA_HOME = 'C:\AndroidDev\jdk\jdk-17.0.2'
$env:ANDROID_HOME = 'C:\AndroidDev\sdk'
$env:GRADLE_USER_HOME = '<workspace>\.tmp\gradle-home'
cd '<workspace>\.tmp\pojav-src-lead-catalog'
.\gradlew.bat assembleDebug --no-daemon
```

产物：`dist/PCL-Mobile-catalog-20261122-r9.apk`（约 38.3 MB）。
包名仍是 `net.kdt.pojavlaunch.debug`，与 `PCL-Mobile-0.1.1.apk` 一致；用 `keystore/pclpro-release.jks`
重新签名后，签名指纹与 0.1.1 相同，可直接覆盖升级（已在模拟器上从 0.1.1 实测升级成功）。

### 5.2 验证证据（Android 7.0 x86 模拟器，真实安装）

- 主菜单出现 **EXTENSIONS**；进入后标题、搜索框、两个标签页、分类、Mod 行、页脚全部渲染正常。
- **安装链路**：把实例 `lastVersionId` 临时改成 `fabric-loader-0.16.9-1.16.5`，点 FerriteCore 的“安装”，
  成功下载 `ferritecore-2.1.1-fabric.jar` 到 `.minecraft/mods/`（109,960 字节）。
- **错误链路**：原版 1.16.5 实例点安装，弹出 “This instance has no mod loader; install Fabric / Forge first”。
- **升级链路**：模拟器先装 `PCL-Mobile-0.1.1.apk`，再覆盖安装 catalog 版，安装成功，
  主菜单出现 **EXTENSIONS**（两包签名指纹一致，无需卸载）。
- **全网搜索链路**：实例 `fabric-loader-0.16.9-1.16.5`，搜索 `sodium`，返回 Sodium / Sodium Extra /
  Reese's Sodium Options / SSPB；点第一项安装，成功下载
  `sodium-fabric-mc1.16.5-0.2.0+build.4.jar`（507,710 字节）到 `.minecraft/mods/`。
- **前置依赖链路**：搜索 `roughly` 安装 REI，自动补全 2 个 required 前置，`mods/` 下得到
  `RoughlyEnoughItems-5.12.385.jar`、`cloth-config-4.17.101-fabric.jar`、`fabric-api-0.42.0+1.16.jar` 三个文件。
  这一步还暴露并修掉了一个过滤 bug：REI 的 `client_side` 是 `optional`，原来的 `client_side:required`
  会把它漏掉，现在改为 `required OR optional`。
- **服务器页**：Hypixel / 2b2t / Wynncraft 正常展示，复制按钮可用，页脚切换为服务器提示。
- **扩充后的实测**：新增 DonutSMP `18631/50000 · 265ms`、CubeCraft `682/5000 · 468ms`、
  OPBlocks `518/1000 · 528ms`、FreshSMP `2297/5000 · 271ms`、TalonMC `2456/5000 · 601ms`、
  Complex Gaming `1343/3000 · 306ms`；Bedrock 新增 VenityMC `49/50 · 460ms`、
  Vortex `—/1168 · 274ms`、AppleMC `—/462 · 415ms`。
- **自定义服务器链路**：添加 `MyTestServer / play.mineville.org / Java` → 列表出现该行并显示
  `Offline / no response`；强制停止并重启 App 后仍存在；长按 → 删除确认 → 行消失，
  `shared_prefs/catalog_custom_servers.xml` 变为 `[]`。
- **已安装/更新链路**：装 FerriteCore 2.1.0 后切到“已安装”，显示
  `v2.1.0 → v2.1.1-fabric` 和“更新”；点更新后 `mods/` 只剩 `ferritecore-2.1.1-fabric.jar`，
  页面变为 `v2.1.1-fabric · 已是最新`。
- **已安装链路**：在 `fabric-loader-0.16.9-1.16.5` 实例装 FerriteCore 后切到“已安装”，
  显示 `ferritecore-2.1.1-fabric.jar / 107 KB`；点 Delete → 确认 → 行消失，
  `mods/` 目录清空。
- 过程中修了一个状态刷新的问题：搜索时列表会重绘，旧实现只更新第一次创建的 TextView，导致一直停在
  “Checking…”；改为按地址登记所有状态视图，结果到达后更新全部可见行。
- **在线状态链路**：Hypixel `21349/200000 · 346ms`、Wynncraft `1327/2000 · 283ms`、
  MCC Island `510/2000 · 374ms`、ManaCube `1254/3500 · 684ms`、PikaNetwork `1120/6500 · 850ms`；
  Bedrock：Galaxite `726/9999999 · 202ms`、Lifeboat `5454/15454 · 482ms`；
  2b2t、The Archon、Zeqa 在模拟器网络下显示 `离线 / 无响应`（超时 2.5s）。
- 模拟器状态已还原（profile 回到 1.16.5，测试 jar 删除）。

截图：

- `docs/screenshots/p7-catalog-mods.png`
- `docs/screenshots/p7-catalog-servers.png`
- `docs/screenshots/p7-catalog-installing.png`
- `docs/screenshots/p7-catalog-install-toast.png`
- `docs/screenshots/p8-catalog-search.png`
- `docs/screenshots/p9-catalog-rei.png`
- `docs/screenshots/p10-catalog-server-status.png`
- `docs/screenshots/p11-catalog-bedrock-status.png`
- `docs/screenshots/p12-catalog-new-server.png`
- `docs/screenshots/p13-catalog-add-server.png`
- `docs/screenshots/p14-catalog-installed.png`
- `docs/screenshots/p15-catalog-mod-update.png`

## 6. 与并行会话的协作

- `PCL Pro 手机版/mobile-fork` 是源码目录，`.tmp/pojav-src` 是并行会话使用的构建树。
- 本轮只改了三处既有文件（Manifest、主菜单布局、MainMenuFragment），且都已确认在 `.tmp/pojav-src` 中保持原样；
  新增字符串全部放进**独立的 `strings_catalog.xml`**，没有动 `values/strings.xml`，避免与并行改动冲突。
- 本轮新增文件和改动已同步到 `.tmp/pojav-src`，并保留该树中其它会话对 `Tools.java` / `LauncherPreferences.java` 的修改。
- 独立的构建副本在 `.tmp/pojav-src-lead-catalog`，未覆盖共享构建产物。

## 7. 桌面端旁路扩展（无源码方案）

`PCL Pro` 本体是编译后的 .NET/WPF 程序，没有源码，改不了 UI；但它启动的是标准 Minecraft 目录，
所以在桌面端做了一套**不碰 PCL Pro 进程**的旁路工具：

- 路径：`C:\Users\Administrator\Desktop\PCL Pro\扩展中心\`
- 文件：`pcl_extend.py`（Python 3 标准库）、`pcl_client_mods.json`、`pcl_servers.json`、`README.md`
- 实例识别：读 `%APPDATA%\.minecraft\PCL.ini` 的 `Version:` 行（当前 `26.3-Fabric`）。
- **Mod**：`mods --list` 逐项查询 Modrinth；`mods --search <关键词>` 直接搜 Modrinth 全量
  （按当前实例过滤）；`mods --install <slug...> --apply` 下载到 `<game>/mods`，
  并递归安装 `required` 前置。默认 **dry-run**，只打印文件名。
- **已安装 / 更新**：`mods --installed` 对每个 jar 算 SHA1 查 Modrinth，显示版本；
  `mods --updates` 只列可更新项；`mods --update` 下载新文件并删除旧文件。
- **服务器**：`servers --status` 用 Java Server List Ping 显示在线人数/延迟；
  `servers --apply` 把 Java 服务器合并进 `<game>/servers.dat`，写入前自动备份。

验证证据：

- `mods --list` 在 `26.3 / fabric` 下 **42 / 48** 可安装（Figura 等 6 个暂无 26.3 构建）。
- 用 `--dest .\staging` 实测安装 `iris ferrite-core`，得到
  `iris-fabric-1.11.7+mc26.3.jar`、`sodium-fabric-0.9.2+mc26.3.jar`（Iris 的 required 前置）、
  `ferritecore-9.0.0-fabric.jar`；真实 `mods/` 未被改动。
- `servers --apply` 后 `servers.dat` 从 4 条变 21 条（新增 17 条 Java；新增条目 `hidden=0` 可见，
  原有 4 条 `hidden=1` 原样保留）；备份在 `servers.dat.bak-20261122-112407`。
- 桌面端更新链路：在 `staging-update/` 放入旧版 `ferritecore-2.1.0-fabric.jar`，
  `mods --installed` 显示 `2.1.0 → 9.0.0-fabric`；`mods --update` 下载 9.0.0 并删除旧文件，
  再查显示 `9.0.0-fabric（已是最新）`。
- 桌面端搜索：`mods --search sodium` 返回 Sodium / Sodium Extra / Reese's Sodium Options 等 12+ 条。
- 桌面端状态：`servers --status` 实测 Hypixel `20003/200000 · 570ms`、Wynncraft `1364/2000 · 241ms`、
  DonutSMP `17143/50000 · 242ms`、OPBlocks `492/500 · 704ms`；2b2t / The Archon 无响应。

## 8. 已知边界 / 下一轮

- **桌面版 `PCL Pro`**：已经有旁路脚本（Mod 安装 + 服务器合并），但 PCL Pro 自己的 UI 仍然没有集成；
  要真正做进界面，需要它的源码。当前方案是“最小侵入”：不改 exe、不注入进程、不碰协议。
- 安装目前只走 **Modrinth**；CurseForge 的单个 Mod 安装还没接（`CurseforgeApi` 已有 classId 6 的搜索能力，可复用）。
- 依赖解析只处理 `required`，`optional` / `incompatible` 不自动装；依赖失败不会中断主 Mod 安装，但也不会提示缺了哪个。
- 目录是“**精选 + 全网搜索**”两层：空搜索看精选，输入 ≥2 字符直接搜 Modrinth 全量（已按实例过滤）。
- `both` 条目只负责“装到自己客户端”，不解决服务端/对方同步。
