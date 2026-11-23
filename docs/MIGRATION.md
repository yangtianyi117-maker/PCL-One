# 迁移与删除旧文件夹

## 1. 旧 → 新 映射

| 旧位置 | 新位置 | 说明 |
|---|---|---|
| `PCL Pro\PCLPro.exe` + `PCLProRuntime` | `vendor\pcl-pro` | 3.0.0 起只取其中 `jdk-25` 作为 Earth Java，启动器本体不再发布 |
| `%APPDATA%\.minecraft`（26.3 / 26.3-Fabric） | `vendor\earth\game` + `vendor\earth\mods` | 由 `tools\prepare-earth-offline.ps1` 抽取；发布为 `dist\PCL One\Earth\.minecraft` |
| `泰坦PCL\Plain Craft Launcher 2.exe` + `.minecraft` + `PCL` | `vendor\titan` | 发布时去掉 `Plain Craft Launcher 2.exe` 与 `PCL\`，只发 `.minecraft` |
| `泰坦PCL\泰坦1.7.10服务端` | `vendor\server` | 唯一服务端副本；发布为 `dist\PCL One\Server` |
| `PCL Pro 手机版\app` + `build.ps1` + `keystore` + `docs` | `src\mobile` | APK 从这里构建 |
| `PCL Pro 手机版\mobile-fork`（源码） | `src\mobile\pojav` | 保留源码；不参与当前 APK 构建 |
| `PCL Pro 手机版\java-launcher`（源码） | `src\mobile\java-launcher` | 保留实验源码 |
| `PCL Earth\client` | `src\earth\client` | 地球安装器 / NBT / payload 模板 |
| `PCL Earth\server` | `src\earth\server` | 地球服务端套件 |
| `PCL Earth\docs` | `src\earth\docs` | 文档 |
| `PCL Earth\tests` | `src\earth\tests` | 测试 |
| `PCL Pro 手机版\dist\PCL-Mobile-*.apk`（Pojav） | `vendor\mobile-pojav` | 保留 APK；源码在 `src\mobile\pojav`，重建需 Gradle/NDK |
| `PCL Earth\dist` | 重新执行 `build.ps1` 生成 | 无需保留 |

## 2. 没有复制过来的东西（删前确认）

以下内容不在 `PCL One` 中，但都可以重建或只是历史产物：

- `泰坦PCL\泰坦1.7.10整合包.zip`（约 267 MB）、`泰坦1.7.10-发给朋友.zip`（约 39 MB）、`给朋友\`：与 `vendor\titan` 内容重复，可用 7-Zip 重新打包。
- `PCL Pro\PCLPro.exe.bak-20261122-2045`：安装器/手工备份。
- `PCL Pro 手机版\dist` 里的历史 APK（1.0.0 / 1.1.0 / PCL-Mobile 0.1–0.3）：可由 `src\mobile` 重建；Pojav APK 需要 Gradle/NDK，当前包不承诺重建。
- `PCL Pro 手机版\mobile-fork\build`、`.gradle`、`java-launcher\build`：构建中间产物，已排除。
- `PCL Earth\dist` 的客户端 zip：`build.ps1` 会重新生成。

如果这些历史文件对你有价值，先打包备份再删。

## 3. 已验证的“可删除”证据

1. **单一配置**：`pcl-one.json` 驱动 EXE（运行时读取）、APK（生成 `OfficialServers.java`）、服务端控制面（`servers.json`/`earth.html`）、安装器（`quickplay.json`/`mods.json`）。
2. **单一构建**：`build.ps1` 只读 `src/`、`vendor/`、`shared/`、`pcl-one.json`。
3. **无旧路径依赖**：`src/`、`tools/`、`build.ps1` 中没有指向四个旧 Desktop 文件夹的功能性路径；安装器仅保留 Desktop 作为最后兜底，包内 `Earth\PCLPro` 优先。
4. **实测**：把四个旧文件夹移出 Desktop 后，在 `PCL One` 内执行完整离线构建（EXE + Earth payload + dist + zip + 单文件 exe）与 25 项自检全部通过。四个旧文件夹现已归档到 `Desktop\_PCL One Archive`，构建与运行不再依赖它们。
5. **离线**：`dist\PCL One` 内的地球客户端、Mod、Java 都在包内；`tools\offline-smoke.ps1` 只用包内文件启动过 26.3 + Fabric，见 `docs\OFFLINE.md`。
6. **无第三方 UI**（3.0.0）：发布目录不含 PCLPro/PCL2；`PCL One.exe` 用 `McLaunch.cs` 直接拼 java 命令行，`--plan`/`--selftest` 可验证。
7. **无任何界面**（4.0.0）：PCL One.exe 不创建窗口，功能由 6 个快捷方式参数触发；单文件 exe 静默解压；设置与日志是文本文件。
8. **游戏内菜单**（6.0.0）：Fabric Mod `pcl-one-menu` 在标题界面加「PCL One」按钮，7 项功能全在游戏菜单里；`--selftest` 会检查该 Mod 是否随包。

## 4. 当前状态与删除

四个旧文件夹现在在 `Desktop\_PCL One Archive`，已不是构建/运行的一部分。下面的脚本仅用于从旧位置再归档一次（幂等）：

```powershell
$desk = "C:\Users\Administrator\Desktop"
$backup = Join-Path $desk "_PCL One Archive"
New-Item -ItemType Directory -Force -Path $backup | Out-Null

# 先移动，不直接删；确认新包运行正常后再删 _PCL One Archive
foreach ($name in @("PCL Pro", "PCL Pro 手机版", "泰坦PCL", "PCL Earth")) {
    $src = Join-Path $desk $name
    if (Test-Path $src) { Move-Item $src (Join-Path $backup $name) -Force }
}
```

确认 `PCL One\dist\PCL One\PCL One.exe` 的快捷方式启动正常后：

```powershell
Remove-Item (Join-Path $desk "_PCL One Archive") -Recurse -Force
```

## 5. 回滚

旧文件夹只是被移动，随时可以移回：

```powershell
Move-Item (Join-Path $backup "PCL Pro") (Join-Path $desk "PCL Pro")
```
