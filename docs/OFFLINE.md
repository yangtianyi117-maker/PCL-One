# 离线包：全部功能在游戏菜单里

`PCL One 6.0.0`：没有第三方启动器界面，也没有外部启动器窗口。
进入 Minecraft 标题界面后，右上角「PCL One」按钮就是全部功能菜单。

## 1. 界面清单

| 位置 | 内容 |
|---|---|
| Minecraft 标题界面 | 原版界面 + 我们的「PCL One」按钮（Fabric Screen API） |
| PCL One 菜单 | 进入地球、泰坦 1.7.10/1.12.2、开泰坦服、离线自检、打开目录、返回 |
| 桌面入口（可选） | 6 个 `.lnk`，各自带参数直接启动对应版本 |
| 安装器 | 无。单文件 exe 静默解压后直接启动 |
| 设置 | `PCLOne-settings.json` 文本文件 |
| 错误/状态 | `PCLOne-error.txt` / `PCLOne-last.txt` / `PCLOne-selftest.txt` |

不存在 PCL、PCL Pro、PCL2、PCL 扩展安装器或任何自绘启动器窗口。

## 2. 游戏内菜单 Mod

`menu/`，Fabric 客户端 Mod，id `pcl-one-menu`：

```text
ScreenEvents.AFTER_INIT（Fabric Screen API）
  └─ TitleScreen → 右上角「PCL One」按钮
       └─ PclOneScreen
            ├─ 进入地球（在线）  → ConnectScreen.startConnecting(HOST:25566)
            ├─ 启动泰坦 1.7.10   → ProcessBuilder("PCL One.exe", "titan1710")
            ├─ 启动泰坦 1.12.2   → ... "titan1122"
            ├─ 开泰坦服          → ... "server"
            ├─ 离线自检          → ... "selftest"
            ├─ 打开综合包目录    → explorer.exe <packageRoot>
            └─ 返回
```

- 无 Mixin，不改 Minecraft 本体；
- 按钮加在标题界面用一次反射调用 `Screen.addRenderableWidget`；
- `packageRoot` 从 `gameDirectory` 向上找 `PCL One.exe` / `pcl-one.json`；
- 菜单失败写 `PCLOne-menu.log`，不弹窗、不崩溃。

构建不需要 Gradle：26.3 客户端 jar 是官方命名类，`tools/build-menu-mod.ps1`
用包内 JDK 25 的 `javac` + `jar`，从 `fabric-api-*.jar` 解出
`fabric-api-base` / `fabric-screen-api-v1` 作为编译期依赖。

## 3. 启动仍然只有四步

```text
版本 JSON（含 inheritsFrom）
  ├─ rules 过滤 libraries → classpath
  ├─ natives 解压
  ├─ 离线账号 UUID/token
  └─ java.exe <jvm> -cp ... <mainClass> <game args>
```

`PCL One.exe` 永远无窗口：读参数、起 java、退出；菜单只是把“选哪个”变成一次点击。

## 4. 离线判据

| 动作 | 现在 |
|---|---|
| 启动地球 | 包内客户端 + JDK 25，0 下载 |
| 启动泰坦 1.7.10 / 1.12.2 | 包内客户端 + Java 8，0 下载 |
| 开泰坦服 | 包内服务端，0 下载 |
| 游戏内菜单 | 包内 Mod，0 下载 |
| 自检 | 25 项：计划、资源、Mod、菜单 Mod、服务器列表、快捷方式、无控制台脚本 |

**仍需要网络**：地球服本体（`124.221.231.213:25566` / `19132`）。
没网时快捷方式启动会按单机；菜单里的「进入地球（在线）」会显示原版连接失败的提示。

## 5. 目录契约

```text
dist/PCL One/
├─ 进入地球.lnk  地球单机.lnk  泰坦 1.7.10.lnk  泰坦 1.12.2.lnk  开泰坦服.lnk  离线自检.lnk\n├─ servers.txt             # 服务器列表（菜单“服务器列表”读取）
├─ PCL One.exe
├─ PCLOne-settings.json     # 可选：用户名 / 内存
├─ Earth/
│  ├─ .minecraft/           # 26.3 + Fabric + assets + libraries + 9 mods（含菜单 Mod）
│  └─ java/jdk-25.0.5+8/
├─ Titan/                   # Forge 1.7.10 / 1.12.2 + Java 8
├─ Server/                  # 1.7.10 Forge 服务端 + 世界
└─ docs/
```

## 6. 通用版本启动

菜单不写死版本：扫描包内所有 `<root>/.minecraft/versions/<id>/<id>.json`，每个实例一个按钮，
点击调用 `PCL One.exe launch <gameDir> <instance>`。Java 按版本 JSON 的 `javaVersion.majorVersion` 自动匹配（26.3→25，1.7.10/1.12.2→8），
在 `<gameDir>\runtime`、同级 `java\`、包内 JDK 中选。
CLI 也可直接调用：

```powershell
外部目录：`games.txt` 每行 `名称|路径`（绝对或相对本包），菜单会一起列出；
`launch` 也接受绝对路径（`PCL One.exe launch D:\mc 1.20.1`）。

PCL One.exe versions
PCL One.exe launch Earth\.minecraft MyFabricInstance
PCL One.exe launch Titan\.minecraft 1.12.2-forge-14.23.5.2859 1.2.3.4:25565 --memory 6144 --username Steve
```

## 7. 验证（2026-11-23 本会话）

```powershell
PCL One.exe --selftest       # 20 项全 OK
PCL One.exe --plan earth     # 82 jars
PCL One.exe --plan titan1710 # 49 jars
PCL One.exe --plan titan1122 # 54 jars
```

- 游戏日志：`Loading 60 mods: ... - pcl-one-menu 1.0.0`，无加载错误；
- 标题界面「PCL One」按钮：截图可见；
- 菜单 7 个按钮：截图可见；
- 菜单「离线自检」：`PCLOne-last.txt` → `command=selftest status=ok`；
- 菜单「启动泰坦 1.7.10」：java 进程启动（49 jars）并持续运行；
- 菜单「进入地球（在线）」：原版连接界面出现。

## 7. 单文件 exe（静默）

`[ PE 引导器 ][ zip ][ "PCL1SFX1" | Int64 偏移 | Int64 长度 ]`

- 双击：解压到 exe 所在目录（不可写则 `%LOCALAPPDATA%\Programs`），启动 `进入地球`；
- 已装过：按 `PCLOne-version.txt` 跳过解压；
- 覆盖安装只覆盖同名文件，不删存档；
- CLI：`--verify` / `--verify-full` / `--extract <dir>` / `--version`，结果写 `PCLOne-sfx.log`。

## 8. 不变量

1. 发布目录没有第三方启动器，也没有外部启动器 UI。
2. 全部功能可从游戏内菜单触达。
3. 零下载：启动路径只读包内文件。
4. 可搬迁：全相对路径，`packageRoot` 自动向上查找。
5. 可验证：`--selftest` / `--plan` / 游戏日志 / 截图。
6. 世界文件不随包：Earth 客户端不含 `saves/region`。
