# PCL One 与手机端的分工（第一性）

PCL One 的离线发行版（`PCL-One`，5.0.0）**不随包附带手机 App**。
手机登录地球服只需要官方 Minecraft 基岩版；需要联机助手/房间码时再装
`PCL-Mobile-Link`。本仓库（PojavLauncher fork）继续作为 Android 上的 Java 启动器
独立存在，不进入离线包。

## 为什么拆开

| 能力 | 运行时 | 仓库 | 是否进 PCL One 离线包 |
|---|---|---|---|
| 桌面启动（Earth/Titan/服务端/游戏内菜单） | Windows + Java 25/8 | [PCL-One](https://github.com/yangtianyi117-maker/PCL-One) | 是 |
| 手机进地球服 | 官方 Minecraft 基岩版 | 无 App（手填地址） | 是（地址） |
| 手机联机助手（官方服/局域网/房间码/地球指南） | Android 8+ | [PCL-Mobile-Link](https://github.com/yangtianyi117-maker/PCL-Mobile-Link) | 否（单独安装） |
| Android 上的 Java 版启动器 | PojavLauncher（GPLv3） | 本仓库 | 否 |

## 三条第一性判断

1. **能不加 App 就不加**：地球服是基岩版协议，手机用官方客户端填
   `124.221.231.213:19132` 就能进；助手只在“一键进入/房间码”时才有价值。
2. **许可证与体积**：PojavLauncher 是 GPLv3 的大体积运行时，放进离线包会改变
   分发义务与包体；PCL-Mobile-Link 是零依赖小工具，可独立发布。
3. **一份源码一个构建**：桌面端由 `pcl-one/pcl-one.json` 驱动 EXE/Mod/服务端；
   手机端由 `PCL-Mobile-Link` 的 `app/` 单独构建 APK；两者只共享服务器地址与指南
   HTML，不互相嵌套。

## 版本

- PCL One：5.0.0（离线包 / 游戏内菜单）
- PCL-Mobile-Link：2.1.0（离线地球指南）
- PCL-Mobile（本仓库）：PojavLauncher fork，发布节奏独立
