# PCL One · 游戏内菜单版（Minecraft 离线 Pro）

**全部功能集成在 Minecraft 自己的菜单里**：标题界面右上角多一个「PCL One」按钮，
点开就是完整功能菜单。没有第三方启动器界面，也没有外部启动器窗口。

| 功能 | 入口 |
|---|---|
| 进入地球（在线）/ 服务器列表 | 游戏菜单 → 进入地球（在线）/ 服务器列表 |
| 版本列表 / 设置（用户名、内存）/ 泰坦 / 开服 / 自检 / 打开目录 | 同一个游戏菜单 |
| 直接启动某个版本 | 目录里的 6 个 `.lnk` 快捷方式（可选） |
| 手机联机 | 官方 Minecraft 基岩版直连 `124.221.231.213:19132` |

## 通用性：任意已安装版本都能启动

不是只认 Earth/Titan。游戏内菜单会扫描包内所有
`<root>/.minecraft/versions/<id>/<id>.json`（以及 `Earth/.minecraft`、`Titan/.minecraft`），
把每个实例列成一页按钮，点哪个启动哪个：

```text
PCL One.exe versions                                  # 生成 PCLOne-versions.txt
PCL One.exe launch <gameDir> <instance> [server] [--java path] [--memory MB] [--username name]
```

- `<gameDir>` 是相对综合包根目录的路径，例如 `Earth\.minecraft`、`Titan\.minecraft`；
- Java 按版本 JSON 的 `javaVersion.majorVersion` 选（26.3→25，1.7.10/1.12.2→8）：在 `<gameDir>\runtime`、同级 `java\`、包内 JDK 中找匹配项；
- 新增一个 Fabric/Forge 实例只需放进任意 `.minecraft/versions/`，菜单刷新即可见；
- 服务器地址可选：`launch Earth\.minecraft MyInstance 1.2.3.4:25565`。
- 外部游戏目录：在 `games.txt` 写 `名称|路径`（绝对路径或相对本包），菜单会把该目录下的版本一并列出；
  `launch` 也接受绝对路径，例如 `PCL One.exe launch D:\mc 1.20.1`。

## 游戏内菜单怎么来的

`src/earth/mod/pclone-menu` 是一个 Fabric 客户端 Mod（`pcl-one-menu`）：

- 用 Fabric Screen API 的 `ScreenEvents.AFTER_INIT` 往原版标题界面加一个按钮；
- 按钮打开 `PclOneScreen`，里面 7 个按钮覆盖全部功能；
- 「进入地球」调用原版 `ConnectScreen.startConnecting`；
- 其它按钮通过 `ProcessBuilder` 调 `PCL One.exe <参数>`（无窗口启动器）拉起泰坦/服务端/自检；
- 不使用 Mixin，不修改 Minecraft 本体，不依赖第三方启动器。

构建不需要 Gradle：26.3 客户端 jar 是官方命名类，`tools/build-menu-mod.ps1`
用包内 JDK 25 + `javac`/`jar` 直接编译，Fabric API 的 screen/base 模块从
`fabric-api-*.jar` 里解出编译期依赖。

## 第一性：菜单只负责“选择”，启动仍是四步

```text
versions/<id>/<id>.json（含 inheritsFrom）
  ├─ rules 过滤 libraries → classpath
  ├─ natives 分类器 → 解压
  ├─ 离线账号 UUID/token
  └─ java.exe <jvm> -cp ... <mainClass> <game args>
```

菜单按钮只是把“选哪个”变成一个点击；真正启动仍由无窗口 `PCL One.exe` 完成。
因此游戏内菜单失败也不会影响启动路径，反之亦然。

## 源码结构

```text
PCL One/
├─ pcl-one.json                  # 地址、端口、实例、版本、java 路径
├─ build.ps1                     # 菜单 Mod + EXE + payload + zip + 单文件 exe
├─ shared/mods.json              # 地球 Mod 清单（固定 sha1）
├─ src/
│  ├─ shell/PCLOne.cs            # 无窗口启动器（参数 → 引擎 → java）
│  ├─ shell/McLaunch.cs          # 启动引擎
│  ├─ shell/PCLOneSetup.cs       # 静默自解压引导器
│  ├─ earth/mod/pclone-menu/     # 游戏内菜单 Mod（Fabric，无 Mixin）
│  ├─ earth/client|server|docs/  # 地球安装器/服务端/文档源码
│  ├─ mobile/                    # 手机 APK 源码（保留，不随包发布）
│  └─ generated/                 # 构建生成物
├─ tools/build-menu-mod.ps1      # javac + jar 构建游戏内菜单
├─ tools/prepare-earth-offline.ps1
├─ vendor/                       # Earth payload / JDK 来源 / 泰坦 / 服务端
└─ dist/
```

## 构建

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Verify
.\build.ps1 -SkipZip        # 不打包
```

构建顺序：生成配置 → **编译游戏内菜单 Mod** → 编译外壳 → 发布 Earth（含菜单 Mod）/
Titan / Server → 写快捷方式 → 自检 → zip → 单文件 exe。

## 交付物

```text
dist/
├─ PCL One 离线包 6.0.0.exe   # 单文件：静默解压 + 启动，无安装界面
├─ PCL One 离线包 6.0.0.zip
└─ PCL One/
    ├─ 进入地球.lnk / 地球单机.lnk / 泰坦 1.7.10.lnk / 泰坦 1.12.2.lnk / 开泰坦服.lnk / 离线自检.lnk
    ├─ PCL One.exe
    ├─ Earth/  Titan/  Server/
    └─ 离线包说明.txt
```

## 怎么证明

```powershell
PCL One.exe --selftest          # 20 项：计划/资源/Mod/菜单 Mod/快捷方式/无控制台脚本
PCL One.exe --plan earth        # 82 jars
PCL One.exe --plan titan1710    # 49 jars
PCL One.exe --plan titan1122    # 54 jars
```

2026-11-23 实测（本会话）：

- 游戏内 Mod 加载：`Loading 60 mods: ... - pcl-one-menu 1.0.0`，无错误；
- 标题界面出现「PCL One」按钮（截图验证）；
- 游戏内菜单 7 个按钮渲染正常（截图验证）；
- 菜单点「离线自检」→ `PCLOne-last.txt` 记录 `command=selftest status=ok`；
- 菜单点「启动泰坦 1.7.10」→ 新 java 进程启动（49 jars），持续运行；
- 菜单点「进入地球（在线）」→ 原版连接界面出现。

## 边界

- 地球世界文件不随包分发（EarthMC 地图许可要求服务器私有）。
- 地球服是公网服务器；没网时快捷方式启动会自动按单机。
- 运行时不带启动器 UI：Minecraft、Java、Forge/Fabric、Mod 只提供游戏与菜单。
- 手机不随包发 App：用官方 Minecraft 基岩版直连服务器地址。
