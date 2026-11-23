# 数据迁移与回滚

## 数据根

合并后所有可写数据在：

```
<PCLPro.exe 所在目录>\PCLProRuntime\PCL\
```

不再使用：

- `%APPDATA%\McLauncher\`
- `%LOCALAPPDATA%\McLauncher\`
- exe 旁 `.state\`

## 旧 McLauncher 数据

一次性导入（首次启动时执行，写标记后不再扫描）：

| 旧文件 | 处理 |
|---|---|
| `<exe>\.state\rooms.json` | 只导入能解析成 `U/` 的码到 `PCL/Shared/link-history.json`；`ML-` 码只计数跳过，不伪造转换 |
| `%APPDATA%\McLauncher\.state\rooms.json` | 同上 |
| `<exe>\.state\config.json` | 不自动导入中继；如需自建中继，在 PCL 设置里填 `LinkRelayServer` |
| `<exe>\.state\auth.json` | 不导入；PCL 使用自己的账号体系，需重新登录/离线登录 |
| `<exe>\.state\servers.json` | 不导入；用 PCL 的服务器列表页 |
| `<exe>\.state\sessions.json` | 丢弃（进程状态，不能跨进程恢复） |

标记文件：`PCL/Shared/legacy-mclauncher-imported.json`。

## 游戏目录

McLauncher 默认 `%APPDATA%\McLauncher\mc`；PCL Pro 使用 PCL 的游戏目录（本机为
`C:\Users\Administrator\AppData\Roaming\.minecraft`）。本计划**不自动搬迁游戏文件**：

- 想复用旧实例：在 PCL 设置里把游戏目录指向旧 `mc`，或用 PCL 的导入功能。
- 不要删除旧目录，直到确认新实例可启动。

## 回滚

1. 桌面 `PCL Pro.bak-2026-11-20` 是替换前的完整备份；恢复时改名回 `PCL Pro`。
2. 桌面 `McLauncher.exe` 未删除；旧版本仍可运行，但不再更新。
3. 源码归档：
   - `archive/mclauncher-2026-11-20/`
   - `archive/pcl-baseline-src/`
4. 新产物：`dist/PCLPro.exe`；部署前先验证干净目录启动。

## 部署注意

- bootstrapper 现在按 `.payload-version` 重新解压代码；`PCL/` 数据不会被删除。
- 替换 `PCLPro.exe` 后，首次启动会覆盖运行时 DLL，保留 `PCL/Shared`、`PCL/Local` 数据。
- CE 自动更新已在 Pro 构建中禁用；不要手动用 CE 更新包替换运行时，否则 Pro 能力会丢失。
