# 贡献指南

## 先读许可

本仓库按上游《PCL 分发有限许可》约束：

- 不得使用 `PCL 3`、`PCL++`、`PCL Pro`、`PCL 某服务器特供版` 等看似官方后续版本的名称；
- 公开发布构建必须启用正版购买劝导（`-PublicRelease`）；
- 界面主色相保持蓝色，不做类似"赞助解锁"的功能。

违反以上任一条的 PR 不会被合并。

## 开发

```powershell
# 先自检工具链（dotnet / zig 需在 PATH，或放到脚本默认位置）
powershell -ExecutionPolicy Bypass -File tools\build-pclpro.ps1 -DryRun

# 构建（不要用 dotnet run 直接跑 UI 项目）
powershell -ExecutionPolicy Bypass -File tools\build-pclpro.ps1

# 单测
dotnet test "tools\pclce\PCL-CE-dev\PCL.Core.Test\PCL.Core.Test.csproj" -p:LangVersion=14
```

## 提交规范

- 一个 PR 只做一件事；改动启动核心（`PCL.Launcher.Core`）请附构建/启动验证日志。
- 不要提交 `bin/`、`obj/`、`dist/`、`PCL/`、`PCLProRuntime/`、`secrets/`。
- 不要提交任何私钥、VPS 凭据、签名密钥。

## 代码风格

- 沿用上游风格；新增 UI 控件参考 `Pages/PageDownload/Comp/MyCompItem.xaml`。
- XAML 自定义事件注意委托签名：`MyButton.Click` 是 `(object, MouseButtonEventArgs)`，
  `MyIconButton.Click` 是 `(object, EventArgs)`——写错会在页面加载时抛 `XamlParseException`。
