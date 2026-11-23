# 开源落地：第一性审计与执行清单

> 日期：2026-11-21　范围：把本仓库整理成可公开发布的 GitHub 项目
> 结论：**已发布** → <https://github.com/yangtianyi117-maker/PCL-Custom>（公开仓库，`main` 分支）

## 0. 第一性：开源到底需要什么

一个"能开源"的仓库只有四个必要条件，其余都是细节：

1. **完整的许可链**：上游许可逐字保留；修改部分必须落在同一许可体系内；有署名与来源声明。
2. **可复现的构建**：任何人拿到源码能在干净机器上构建出等价产物（SDK 版本、构建参数、依赖来源都要写明）。
3. **零秘密**：仓库里不存在私钥、口令、token、个人数据；历史里也不能有。
4. **可追溯的历史**：从上游某个明确版本分叉，之后每个改动可解释。

本仓库的现状与差距见下。

## 1. 许可审计（《PCL 存储库合理使用指南》重度使用 5 条）

| # | 要求 | 现状 | 本轮动作 |
|---|---|---|---|
| 1 | 声明第三方二次创作 | 之前只有零散注释 | `README` / `NOTICE` / 关于页文案已写明 |
| 2 | 名称以 `Plain Craft Launcher (PCL)` 开头 + 修改后缀，禁止 `PCL Pro` | 🟡 显示名已改；exe/更新通道待改 | 15 处用户可见字符串 → `PCL 定制版`；关于页新增"第三方修改版"声明；`PCLPro.exe` 与 `/pclpro/` 作为技术标识暂留 |
| 3 | 关于页首位署名龙腾猫跃 + 赞助链接；公开源代码；保留指南文件 | ✅ 已满足 | 关于页第 0 行即龙腾猫跃 + 赞助入口（上游原有）；新增第 3 行第三方声明；根目录 `LICENSE` + `LICENCE` |
| 4 | 启动功能需有与 `McLaunchPrecheck` 一致的正版/赞助劝导 | ❌ 之前被我们在 `ProEdition` 构建里短路 | **已改为构建开关**：`-PublicRelease` 定义 `PUBLIC_RELEASE`，公开构建恢复原劝导；自用构建不弹 |
| 5 | 不做类似赞助解锁的功能；主色相蓝色 | ✅ 已满足 | 无 |

第 4 条的实现位置：

- `PCL.Launcher.Core/Launch/Core.cs`：`_ShowPurchaseHint()` 的调用被 `#if PUBLIC_RELEASE` 包裹
  （自用构建不弹，公开构建与上游一致）。
- `Modules/Minecraft/ProfileUi.cs`：`minecraft_not_owned` 的购买入口恢复原样。
- `Plain Craft Launcher 2.csproj`：`PublicRelease=true` 时追加 `PUBLIC_RELEASE` 常量。
- `tools/build-pclpro.ps1`：新增 `-PublicRelease` 开关。

> **执行结果**：15 处 `"PCL Pro"` 字面量已替换为 `"PCL 定制版"`（12 个文件），
> 关于页新增"第三方修改版"声明行；exe 名与更新通道保持 `PCLPro` / `/pclpro/` 作为技术标识，发布前可再统一。
>
> 这解决了之前的冲突：你要"启动时别弹购买窗口"，许可是"公开发布必须有劝导"——
> 现在两者按构建用途分开，自用不弹、公开必弹。

## 2. 改名矩阵（131 处 / 60 文件）

最终名称：**PCL 定制版 / PCL Custom**（`PCL` + 修改后缀；仓库名 `PCL-Custom`）。
2026-11-21 收短过一次：原「PCL 个人定制版 / PCL Personal Custom Edition」→「PCL 定制版 / PCL Custom」。

| 类别 | 位置 | 示例 | 处理 |
|---|---|---|---|
| 显示名 | `PCL.Core/App/ProEdition.cs` `DisplayName` | `"PCL Pro"` | 改 |
| 窗口标题 | `FormMain.xaml(.cs)`、`Setup/UI.cs` | `LabTitleLogo.Text` | 改 |
| 版本串 | `PageSetupUpdate*`、`ProUpdateService` UA | `PCLPro/1.0` | 改 |
| 更新通道 | `Config.ProUpdateUrl`、VPS `/pclpro/` | `http://…/pclpro/` | 改目录（旧地址留 301） |
| exe 名 | `PCLPro.exe`、bootstrapper、release 脚本 | — | 可选（技术标识可保留，但发布物最好一致） |
| 代码标识 | 命名空间/类名 `PclPro*`、`ProEdition` | — | 可保留（不影响用户可见名称） |
| 文档 | `docs/**`、`README`、`HISTORY.md` | — | 随发布改 |

执行方式（待确认名称后）：

```powershell
# 仅改用户可见字符串，不动内部标识，避免大范围回归
$files = Get-ChildItem tools\pclce\PCL-CE-dev -Recurse -Include *.cs,*.xaml |
  Where-Object FullName -notmatch '\\(bin|obj)\\'
$files | Select-String -Pattern '"PCL Pro"' -List | ForEach-Object { $_.Path }
```

## 3. 秘密清单（已在 `.gitignore` 排除）

| 资产 | 路径 | 状态 |
|---|---|---|
| 更新签名私钥（含 `d`） | `secrets/pclpro-update-key.json` | 排除；建议移到 CI Secrets |
| VPS 口令 | 环境变量 `PCL_VPS_PASSWORD` | 代码未硬编码（已扫描：0 命中） |
| VPS 主机/路径 | `release/config.json` | 可公开（无口令） |
| 运行时数据 | `PCL/`、`PCLProRuntime/` | 排除 |
| 构建产物 | `dist/`、`build/`、`.nuget/`、`.dotnet-home/` | 排除 |

已做定向扫描（`secrets/ release/ tools/research/ tools/_archive/ docs/`）：**0 处口令/私钥泄漏**。

## 4. 仓库落地（已完成）

公开仓库：<https://github.com/yangtianyi117-maker/PCL-Custom>（`main`，public）。

```powershell
git remote add origin https://github.com/yangtianyi117-maker/PCL-Custom.git
git push -u origin main
```

仓库名按《合理使用指南》取了 `PCL` 前缀 + 第三方后缀（`PCL-Custom`），没有沿用 `PCL Pro`
（指南 §二.2 明确把 `PCL Pro` 列为禁止名称）。开发期代号 `PCL Pro` 仅保留在内部文档与历史说明里。

注意：
- `docs/research/servers-all.json` 等大文件已 ignore，需要时发 Release 附件；
- 推送前确认 `git status` 里没有 `secrets/`、`dist/`、`PCL/`。

## 5. CI（已生效）

`.github/workflows/build.yml`：push / PR 触发，`windows-latest` + .NET 10，跑 private 构建、
`-p:PublicRelease=true` 构建与 `PCL.Core.Test`。首次推送已跑通（success）。

发布（已完成）：`v1.0.0` Release 附 `PCL-Custom-v1.0.0-win64.zip` + `SHA256SUMS.txt`
（`-PublicRelease` 构建，内部版本 `2026-11-21.17` / BuildNumber 26）。
后续若接 CI 自动发版，再把签名与上传 Release 搬进 Actions（私钥走 CI Secrets）。

## 6. 验收

| 项 | 证据 |
|---|---|
| 许可文件落位 | 根目录 `LICENSE`(11,513 B) + `LICENCE`(4,756 B) |
| 声明文件 | `NOTICE` / `README.md`（英文主文档）/ `README.zh-CN.md` / `CONTRIBUTING.md` / `SECURITY.md` |
| 秘密排除 | `.gitignore` 覆盖 `secrets/`、`PCL/`、`dist/` 等；定向扫描 0 命中（历史 blob 同样扫过） |
| 公开构建开关 | `-PublicRelease` → `PUBLIC_RELEASE` → 恢复购买劝导；默认构建不弹 |
| 编译验证 | GitHub Actions `build` 成功；本地 `-PublicRelease` 全量构建（dotnet publish + Zig bootstrapper）成功 |
| 构建可复现 | `tools/build-pclpro.ps1` 支持 `dotnet`/`zig` 走 PATH、`-DryRun` 自检；EasyTier 缺失时跳过 |
| 显示名实测 | 窗口标题 = `PCL 定制版`；关于页首行龙腾猫跃 + 赞助入口，第 4 行「PCL 定制版（本版本）— 第三方修改版」 |
| Git 仓库 | 公开仓库 `main` 分支，`git status` 干净；`git grep` 未发现口令/私钥（仅路径引用） |
| 仓库元数据 | 描述、topics（minecraft/launcher/wpf/dotnet/pcl…）、homepage 已设置 |
