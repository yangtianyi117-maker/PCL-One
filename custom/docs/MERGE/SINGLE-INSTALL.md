# 唯一安装（Single Install）不变量

> 日期：2026-11-21　状态：已执行并验证
> 目的：把「PCL Pro / PCL Pro 备份 / McLauncher」三个东西永久收敛为一个可执行产品，
> 并把这一收敛写成可机器校验的不变量，防止再次分叉。

## 0. 结论

桌面上只有**一个**启动器：`C:\Users\Administrator\Desktop\PCL Pro\PCLPro.exe`。
它下面只有**一个**运行时（`PCLProRuntime\`）和**一个**数据根（`PCLProRuntime\PCL\`）。
McLauncher 的可保留部分（存档、模组、Geyser）已融合进 PCL Pro 的数据根/游戏根，其余已删除。

## 1. 唯一产物结构

```
C:\Users\Administrator\Desktop\PCL Pro\
├── PCLPro.exe                         # bootstrapper，117,907,456 B
│                                      # SHA-256 B39824BFFA31A005584BFAD43AFADD014E3503200D0006697C0C3D527660D8CF
└── PCLProRuntime\
    ├── .payload-version               # pclpro-2026-11-21.10（构建 19）
    ├── Plain Craft Launcher 2.exe      # 真正的 UI 进程
    ├── *.dll / zh-* / cs / de / ...    # .NET 10 + WPF 运行时
    └── PCL\                            # 唯一数据根（便携模式）
        ├── config.v1.yml               # 本地配置（窗口 1350×1084，实例 26.3-Fabric）
        ├── Shared\                     # 全局配置 + link-history.json
        ├── Log\                        # 32 份启动日志（含 McLauncher 时期）
        ├── ModInbox\  Musics\  Pictures\  HostKey\  Local\EasyTier\2.6.4\
        └── runtime\jdk-25.0.5+8\       # 内置 JDK 25
```

游戏根：`C:\Users\Administrator\AppData\Roaming\.minecraft\`（唯一被 PCL Pro 使用的游戏目录）。

## 2. 融合（保留价值，不保留形态）

| 来源 | 内容 | 去处 | 校验 |
|---|---|---|---|
| `%AppData%\McLauncher\mc\saves\New World` | 20.3 MB / 43 文件，原版 26.3 世界 | `...\.minecraft\saves\New World` | 43/43 文件 MD5 逐个相同 |
| `%AppData%\McLauncher\mc\mods` | sodium-fabric-0.9.3、ViaFabricPlus-5.1.3 | `...\.minecraft\mods` | 文件名一致 |
| `%LocalAppData%\McLauncher\assets\Geyser-Standalone.jar` | 62 MB 基岩桥 | `PCL\Geyser\Geyser-Standalone.jar` | 头 4 字节 `PK..`，长度一致 |
| `PCL Pro.bak-2026-11-20\...\PCL\Log` | 16 份早期日志（含 11-21 07:56 那次） | `PCL\Log` | 只补缺，不覆盖 |
| `PCL Pro.bak-2026-11-20\...\PCL\config.v1.yml` | 较新的窗口状态（1084 高） | `PCL\config.v1.yml` | 启动日志回显 1350×1084 |
| `archive\mclauncher-2026-11-20\README.md`、`DESIGN.md` | 旧启动器设计记录（58 KB） | `docs\MERGE\mclauncher-archive\` | 文件存在 |

## 3. 删除（对抗统一的部分）

| 路径 | 大小 | 理由 |
|---|---|---|
| `Desktop\PCL Pro.bak-2026-11-20\` | 618.2 MB | 旧构建（9F53CABB…，2026-11-19）整套安装，数据已融合 |
| `Desktop\PCL Pro\PCLPro.exe.old` | 112.4 MB | 构建 16（B8908C0C…） |
| `Desktop\PCL Pro\PCLPro.exe.12.bak` | 112.4 MB | 构建 13（50C1CD61…） |
| `Desktop\PCL Pro\PCLPro.exe.8.bak` | 112.4 MB | 构建 8（EE012625…） |
| `Desktop\PCL Pro\PCLPro.exe.bak-2026-11-20` | 112.1 MB | 与备份安装同一旧构建 |
| `Desktop\McLauncher.exe` | 130 MB | 失败的独立启动器，能力已并入 PCL Pro |
| `Desktop\.state\` | 0.9 MB | McLauncher 房间/会话状态（仅有 `ML-` 旧码，按设计不可导入 `U/` 房间） |
| `%AppData%\McLauncher\` | 1051.8 MB | 旧游戏目录（世界/模组/Geyser 已融合） |
| `%LocalAppData%\McLauncher\` | 93 MB | EasyTier 2.6.4 二进制（PCL 已有同版本）+ Geyser |
| `archive\mclauncher-2026-11-20\` | 617.2 MB | 旧启动器源码归档（README/DESIGN 已保留） |

合计回收约 **2.7 GB**。

### 3.1 构建产物也收敛成一份

`dist\` 里原本堆着 156 个历史尝试（`bridge-test*`、`overlay-final*`、`native1-7`、`PCLPro-*-folder`、
`easytier-selftest*`、`McLauncher-*.zip`、11 个旧签名包……），合计 **25.5 GB**，都是同一个产品的失败分支或旧版本。
清理后 `dist\` 只保留：

| 保留 | 理由 |
|---|---|
| `dist\PCLPro.exe` | 当前构建（与安装哈希一致，发版校验的基准） |
| `dist\PCLPro-update-2026-11-21.8-signed\` + `.zip` | 当前签名更新包（`release\verify.ps1` 取最新签名目录） |
| `dist\PCLPro-第一版验收.md` | 第一版验收记录（唯一保留的历史文档） |

同时清掉 `build\pclpro\`（384 MB 发布暂存目录，构建时自动重建），`artifacts\` 的构建/测试日志保留。

## 4. 明确「不」融合的三类（并说明为什么）

1. **`%AppData%\PCL\`（0.4 MB）**：官方 PCL2 自己的配置目录，不属于这三个东西，动它会伤别的安装。
2. **McLauncher 的 `servers.dat`**：唯一一条是 `127.0.0.1:25600`（旧剧本的本地中转，已失效），
   而 `.minecraft\servers.dat` 已有 4 条真实服务器；写入等于引入死条目。
3. **McLauncher 的 `26.3-forge-66.1.8` 实例 + libraries**：`.minecraft\libraries` 里没有 Forge 库，
   单独搬版本目录会得到一个启动即崩的实例；该实例在 `tools\pclce\...\mc\` 仍有完整副本，
   需要时用 PCL Pro 重新安装 66.1.9 更干净。

## 5. 不变量（必须一直成立）

1. `Desktop\PCL Pro\PCLPro.exe` 存在，且**安装目录内只有这一个启动器可执行文件**（无 `.bak` / `.old` / `.exe.*`）。
2. 桌面上不存在 `PCL Pro.bak*` 之类的第二套安装。
3. 桌面上不存在 `McLauncher.exe` / `.state`；`%AppData%`、`%LocalAppData%` 下不存在 `McLauncher`。
4. `PCLProRuntime\.payload-version` = `pclpro-2026-11-21.10`（或更新，由发布脚本统一升级）。
5. `PCLProRuntime\PCL\` 是唯一数据根；`config.v1.yml`、`Shared\`、`Log\` 都在其中。
6. `PCL\Geyser\Geyser-Standalone.jar` **不存在**（手机桥已移除：手机直接连固定服务器 `19132`）。
7. `.minecraft\saves\New World\level.dat`、`.minecraft\mods\*.jar` 仍在（融合后的用户资产）。
8. 安装的 `PCLPro.exe` 与 `dist\PCLPro.exe` 哈希一致（安装 = 发布产物，避免两套代码）。

## 6. 校验方式

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify-single-install.ps1
```

脚本逐条打印 `PASS/FAIL`，任一 `FAIL` 以退出码 1 结束，可直接挂到发布脚本末尾当闸门。

已接入发版验证：`release\verify.ps1` 每次跑完签名包/远端校验后，会再调用本脚本，
把 `single install invariant` 作为一条发版闸门（`release\verify.cmd` 一键执行）。

## 7. 变更历史

- 2026-11-21：三合一执行。删除 2 套旧构建 + 4 个旧 exe + McLauncher（exe/状态/AppData）+ 源码归档；
  融合世界、模组、Geyser、日志、窗口配置；启动验证 `程序已退出，返回值：Success`。
- 2026-11-21（同日第二步）：`dist\` 从 156 项 / 25.5 GB 收敛到 4 项（当前 exe + 当前签名包 + 验收文档），
  删除 `build\pclpro` 暂存；`tools\verify-single-install.ps1` 12/12 PASS，并接入 `release\verify.ps1` 发版闸门。
  全盘回收约 **28 GB**。
