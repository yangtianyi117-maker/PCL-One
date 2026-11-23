# 第一性：为什么是「一个壳 + 两套运行时」

> **3.0.0 变更**：本文 1–9 节记录的是「用第三方启动器做运行时」的设计演进。
> 从 3.0.0 起，发布包不再包含 PCL Pro / PCL 2.99，启动逻辑改为
> `launcher/McLaunch.cs` 的原生实现（版本 JSON → classpath → java）。
> 见第 10 节与 `OFFLINE.md`。

## 1. 需求拆解

用户要的不是「一个更大的启动器」，而是四件互相冲突的事同时成立：

1. **现代地球服**：Minecraft 26.3 + Fabric + Java 25 + Java/Bedrock 互通。
2. **泰坦降级**：Minecraft 1.7.10 + Forge 10.13.4 + Java 8 + TheTitans/Techguns。
3. **泰坦服务端**：同一套 1.7.10 mod 的专用服务端与世界。
4. **手机端**：地球服的 Bedrock 入口与联机助手。

「综合包」的直觉是把它们塞进一个 `.minecraft`、一个启动器、一个 `mods/`。这在第一性上不成立。

## 2. 为什么不能合并成一个游戏目录

| 维度 | 26.3 Earth | 1.7.10 Titan | 合并后果 |
|---|---|---|---|
| Java | 25 | 8 | 一个进程只能用一个 JVM；PCL 虽可按实例选 Java，但库/资源仍共享 |
| 模组加载器 | Fabric | Forge 10.13.4 | `mods/` 混装直接崩溃；隔离目录是唯一安全解 |
| 渲染/库 | LWJGL 3、现代 ASM/Mixin | LWJGL 2、旧 Guava/ASM | `libraries/` 可共存但版本解析与 native 冲突概率高 |
| 资源索引 | 26.3 assets | 1.7.10 assets | 同一 `assets/` 可共存，但体积翻倍、校验复杂 |
| 启动器配置 | PCL Pro（PCL CE 2.15，漫游 `%APPDATA%`） | PCL 2.99（便携 `.minecraft` + `PCL`） | 两套配置模型；强行统一会破坏其中一套 |
| 地图许可 | EarthMC 世界仅服务器私有 | 本地/朋友服 | 不能把两个世界混进同一个分发包 |

结论：**合并的是「入口」和「分发」，不是「运行时」。**

## 3. 正确抽象

把综合包看成一个 **发行版（distribution）**，而不是一个 **运行时（runtime）**：

```
PCL One.exe（壳）
├─ 选择/切换配置
├─ 拉起 Earth\PCLPro（现代运行时）
├─ 拉起 Titan\PCL 2.99（降级运行时）
├─ 拉起 Server\start.bat（1.7.10 服务端）
└─ 分发 Mobile APK / EarthExtension
```

壳只做四类动作：

1. 改 `Titan\.minecraft\PCL.ini` 的 `Version:`（选择 1.7.10 / 1.12.2）；
2. `Process.Start` 对应启动器；
3. 调官方安装器（地球扩展）；
4. 打开目录/文档/APK 安装。

不修改任何 PCL 二进制，不写游戏存档，不碰世界文件。

## 4. 不变量

1. **单一入口**：用户只需要知道 `PCL One.exe`。
2. **运行时隔离**：每套运行时拥有自己的启动器、游戏目录、Java、mods；互不写对方。
3. **可逆**：删除 `PCL One` 不影响原 `PCL Pro` / `泰坦PCL`；地球安装器有备份与卸载脚本。
4. **离线可用**：整包（地球客户端 + 泰坦 + 服务端 + 手机）安装与启动零下载；地球服本体在公网，需要网络。
5. **合规**：不分发 Minecraft 本体、不分发 EarthMC 世界、不修改 PCL 二进制。

## 5. 代价

- 磁盘约 **1.6 GB**（两套 PCL + 两套游戏库 + 服务端）。
- 两套启动器各自更新；壳不需要跟着更新。
- 手机只有地球服助手；泰坦手机端需要 Pojav 类运行时，不在本包范围。

## 6. 失败模式与处理

| 失败 | 表现 | 处理 |
|---|---|---|
| 地球扩展未装 | 点「进入地球」提示 | 壳引导运行安装器 |
| 地球安装器失败 | 弹窗/退出码 2–5 | 恢复 `pcl-earth\backup-*`，重试 |
| 泰坦 Java 8 缺失 | PCL 报 Java 错误 | 检查 `Titan\.minecraft\runtime\java-8`；壳自检会标 MISSING |
| 端口 25565 被占 | 服务端启动失败 | 改 `Server\server.properties` 的 `server-port` |
| APK 安装失败 | adb 报错 | 手动复制 APK 到手机安装 |

## 7. 与「单 EXE」的关系

「一个 EXE」指**一个入口 EXE**，不是把两个 PCL 编译进一个进程。把 1.7.10 与 26.3 塞进同一进程/同一游戏目录，违背上面的隔离不变量，收益只是少一个文件，代价是两套运行时都变得不可维护。

## 8. 代码融合：单一配置 + 单一构建

上一版是「四个文件夹 + 一个壳」。这一版把**代码与配置**收敛到 `PCL One`：

```text
pcl-one.json
  ├─ tools/gen-config.ps1
  │    ├─ OfficialServers.java  (APK 常量)
  │    ├─ servers.json          (桌面联机页 / 服务端控制面)
  │    ├─ earth.html            (手机指南)
  │    ├─ mods.json             (安装器 Mod 清单)
  │    └─ quickplay.json        (安装器默认地址)
  ├─ PCL One.exe                (运行时读取：入口/实例/版本切换)
  └─ build.ps1                  (APK 版本、发布目录、PACKAGE.json)
```

效果：

- 地址/端口/实例名只改 `pcl-one.json` 一处，EXE、APK、服务端控制面、安装器全部同步；
- 手机 APK 不再手写常量，构建前由模板生成 `OfficialServers.java`；
- 地球安装器的默认主机/实例来自生成的 `payload/quickplay.json`；
- 服务端 `servers.json` / `earth.html` 由同一配置生成；
- 发布目录 `dist/PCL One` 只由 `vendor/` + `launcher/`、`menu/`、`earth/`、`mobile/` + 构建物组成。

可删除旧文件夹的判据：

1. 所有**可重建**的东西都在 `launcher/`、`menu/`、`earth/`、`mobile/`（壳、APK、地球客户端/服务端、文档）；
2. 所有**不可重建**的东西都在 `vendor/`（PCL Pro、泰坦、服务端世界）；
3. 构建脚本不引用任何旧 Desktop 路径；
4. 已在旧文件夹改名后完成一次全量构建与自检。

这四条同时成立时，旧文件夹只是历史副本，可以删除。

## 9. 离线包：把「下载」从路径上删掉

`2.1.0` 把「离线」从愿望变成发行形态。判据不是「断网能启动」，而是**首次使用路径上没有网络请求**。

```text
离线包
├─ Earth/.minecraft        26.3 + Fabric + assets + libraries + mods（客户端本体）
├─ Earth/PCLPro            便携启动器 + JDK 25 + Geyser + EasyTier
├─ EarthExtension/payload  8 个 Mod jar（安装器只复制，不查询）
├─ Titan / Server          原本就便携
└─ Mobile/*.apk            地球指南在 assets，不走 URL
```

三件事让「便携」成立：

1. `pcl-one.json` 是唯一配置，`gen-config.ps1` 把地址/实例/MC 版本同时写进 EXE、APK、安装器、服务端控制面；
2. PCL Pro 的 `LaunchFolderSelect` 支持 `$`（启动器自身目录），整包换路径仍成立；外壳启动时再兜底改写成绝对路径；
3. 所有会联网的默认开关（启动器更新、首次网络探测）在发布配置里关闭，网络只用于真正需要它的动作：进地球服。

代价是发行体积：Earth 客户端约 626 MB（主要是 478 MB assets 与 290 MB JDK），整包 zip 约 1.5 GB。
收益是**可复现**：`tools/prepare-earth-offline.ps1` 从任意 `.minecraft` 重建 payload，
`tools/offline-smoke.ps1` 只用包内文件真实启动一次游戏，离线结论有硬证据而不是口头承诺。

## 10. 3.0.0：把第三方启动器 UI 也删掉

「综合启动器」曾经等于「壳 + 两个第三方启动器」。第一性回看：

- 第三方启动器提供的是**运行时编排**（读 JSON、解压 natives、拼参数、选 Java），
  不是游戏内容；
- 它同时带来自己的 UI、下载页、更新器、公告、遥测、配置格式；
- 而本产品的差异化功能只有四个：进地球、开泰坦 1.7.10/1.12.2、开服务端、发手机端。

结论：把「编排」拿回来自己实现，UI 只保留功能入口。

```text
McLaunch.cs
├─ 读版本 JSON，沿 inheritsFrom 合并
├─ rules 过滤（windows x64 / features 一律不启用）
├─ libraries → classpath，natives 分类器 → 解压
├─ 离线账号（OfflinePlayer:<name> 的 v3 UUID）
└─ java.exe -cp ... <mainClass> <args>
```

发布包因此删掉约 640 MB 的 PCLProRuntime 与 PCL2，只保留 JDK 25（地球）
与 Java 8（泰坦）。副作用与取舍：

| 取舍 | 结果 |
|---|---|
| 不再有实例管理/下载/整合包导入 | 本产品本来不需要 |
| 不再有 PCL 的联机房间 UI | 泰坦用 EasyTier + 地址脚本；地球是公网服 |
| 不再自动更新 | 离线包本来就该整包替换 |
| 需要自己维护版本解析 | 约 500 行，`--plan`/`--selftest` 可验证 |

可验证性没有下降：`--plan` 证明解析正确（库数量与 Java 主类），
`--selftest` 证明包内文件齐全，`--launch` 与 `offline-smoke.ps1` 证明真的能进游戏。

## 11. 4.0.0：连自己的启动器界面也不要

3.0.0 把第三方启动器换成了自绘的 PCL One 窗口。问题在于：

- 用户要的是「玩 Minecraft」，不是「用启动器」；
- 每个功能一个按钮，本质是把命令行参数藏在按钮后面；
- 自绘 UI 还要维护主题、布局、DPI、错误弹窗。

所以 4.0.0 把界面也删掉：

```text
进入地球.lnk      -> PCL One.exe earth
地球单机.lnk      -> PCL One.exe earth-local
泰坦 1.7.10.lnk   -> PCL One.exe titan1710
泰坦 1.12.2.lnk   -> PCL One.exe titan1122
开泰坦服.lnk      -> PCL One.exe server
离线自检.lnk      -> PCL One.exe selftest
```

`PCL One.exe` 编译为 winexe，但任何代码路径都不创建窗口；它解析参数、
启动 java、写 `PCLOne-last.txt`，然后退出。设置放进
`PCLOne-settings.json`，错误放进 `PCLOne-error.txt`。
单文件 exe 也改成静默解压（无向导、无进度窗口）。

代价是「没有反馈」：启动失败不会弹窗，只能看日志。这与「没有界面」是同一件事，
文档把日志路径写成契约。

## 12. 6.0.0：把功能放回游戏自己的菜单

4.0.0 的「无界面」把功能留在 6 个快捷方式里。问题：玩家还是要离开游戏去选版本、
开服务端、看自检；而「选哪个」本来就可以在游戏内完成。

6.0.0 增加一个 Fabric 客户端 Mod（`pcl-one-menu`），第一性上只做两件事：

1. 往原版 `TitleScreen` 加一个按钮（Fabric Screen API，无 Mixin）；
2. 按钮打开一个 7 项的 `Screen`，每项调用现成能力：
   `ConnectScreen`（进地球）或 `ProcessBuilder("PCL One.exe", arg)`（泰坦/服务端/自检）。

因此：

- 不需要第三方启动器 UI，也不需要自己画启动器窗口；
- 启动路径不变（仍然是无窗口 EXE → 版本 JSON → java），菜单只是入口；
- Mod 可独立失败：加载失败时快捷方式仍可用；菜单动作失败写
  `PCLOne-menu-error.txt`，不拖垮游戏；
- 构建不引入 Gradle：26.3 是官方命名类，`javac` 直接对着客户端 jar 编译。

取舍：菜单只存在于 26.3（Fabric）客户端；泰坦 1.7.10/1.12.2 的 Forge 菜单
没有对应实现，仍从同一个菜单或快捷方式启动。这是版本/加载器成本，不是产品边界。






## 13. 6.0.0：从两个预设变成通用启动器

5.0.0 的菜单只有 7 个写死的按钮（地球/泰坦/服务端/自检）。
真正通用的抽象是「任意 gameDir + 任意 instance」：

```text
PCL One.exe launch <gameDir> <instance> [host:port]
```r

- 菜单扫描包内所有 .minecraft/versions/<id>/<id>.json，分页列出；
- Java 自动匹配：实例自带 runtime → 同级 java/ → 包内 JDK；
- 地球/泰坦只是两个默认 gameDir，不再参与启动逻辑；
- 新增整合包/版本不需要改代码，只要文件在包里。
