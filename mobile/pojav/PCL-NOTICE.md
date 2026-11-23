# PCL 定制版 手机端（PCL-Mobile）

这是 **PojavLauncher** 的 fork，用来把 PCL 的体验带到 Android 上。

- 上游：https://github.com/PojavLauncherTeam/PojavLauncher （GPLv3）
- 本仓库：https://github.com/yangtianyi117-maker/PCL-Mobile
- 许可：**GPLv3**（见 `LICENSE`）

## 相对上游的修改

1. 应用名：`PCL 定制版 手机端`（`app_name` / `app_short_name`）；
2. 启动图标替换为 PCL 图标；
3. `MinecraftDownloader.downloadFile()`：移除“仅 Microsoft 账号可下载”的限制，
   本地/离线账号也可以下载公开的 Minecraft 文件（PCL 的离线优先理念）；
4. 其余代码与上游一致。

## 构建

需要 JDK 8（`jre_lwjgl3glfw` 的 toolchain）与 Android SDK/NDK：

```bat
gradlew.bat :app_pojavlauncher:assembleDebug
```

产物：`app_pojavlauncher/build/outputs/apk/debug/app_pojavlauncher-debug.apk`

## 发布包

我们的签名包在 GitHub Releases 中发布，证书为 `CN=PCL Pro Mobile`。

## 说明

- 本项目不包含 Microsoft 登录所需的 client id，也不分发 Minecraft 本体；
- 请使用正版账号登录，或使用离线账号自行承担相应责任。
