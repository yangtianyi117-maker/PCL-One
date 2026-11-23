using System;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.App.Cli;
using PCL.Core.Link.Phone;
using PCL.Core.UI.Hints;

namespace PCL;

/// <summary>
/// pclpro:// 深链的应用层路由：
/// 第一实例直接处理命令行；第二实例由 SingleInstanceService 通过 RPC 转发到 join-code 回调。
/// 处理的最终结果只是“把房间码放进联机页的输入框”，不自动启动游戏，避免误触。
/// </summary>
public static class ProDeepLink
{
    private static string? _pendingCode;

    public static string? PendingCode => _pendingCode;

    /// <summary>在生命周期启动前注册回调；不解析参数，只保证 RPC 到来时能接住。</summary>
    public static void Initialize()
    {
        StartupService.TryHandleCommand("join-code", Handle, registerCallback: true);
    }

    /// <summary>命令行参数解析完成后调用一次，处理本次启动携带的 URL。</summary>
    public static void Flush()
    {
        if (!Config.Link.DeepLinkEnabled) return;
        StartupService.TryHandleCommand("join-code");
    }

    public static string? ConsumePending()
    {
        var code = _pendingCode;
        _pendingCode = null;
        return code;
    }

    private static void Handle(CommandLine model, bool isCallback)
    {
        var (exists, isTypeMatch) = model.TryGetArgumentValue<string>("url", out var url);
        if (!exists || !isTypeMatch || string.IsNullOrWhiteSpace(url))
        {
            Common.Log("[深链] join-code 缺少 --url 参数");
            return;
        }

        if (!DeepLinkRouter.TryParse(url, out var link))
        {
            Common.Log("[深链] 无法解析：" + url);
            return;
        }

        _pendingCode = link.Code;
        Common.Log($"[深链] 收到房间邀请：{link.Code}");
        HintService.Show(Hints.Info($"收到房间邀请：{link.Code}。打开“工具 → 联机”即可加入。"));
    }
}
