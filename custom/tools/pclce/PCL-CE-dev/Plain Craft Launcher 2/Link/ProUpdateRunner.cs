using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.Updates;
using PCL.Core.UI.Hints;

namespace PCL;

/// <summary>
/// 启动时自动检查 PCL 定制版 签名更新：静默下载、验签、替换 bootstrapper，
/// 只提示“重启生效”，不打断正在运行的游戏。
/// </summary>
public static class ProUpdateRunner
{
    private static int _started;

    public static void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) return;
        if (!Config.Link.ProUpdateAuto) return;

        var url = Environment.GetEnvironmentVariable("PCL_PRO_UPDATE_URL");
        if (string.IsNullOrWhiteSpace(url))
            url = Config.Link.ProUpdateUrl;
        if (string.IsNullOrWhiteSpace(url)) return;

        _ = Task.Run(() => CheckAsync(url));
    }

    private static async Task CheckAsync(string baseUrl)
    {
        try
        {
            var info = await ProUpdateService.CheckAsync(baseUrl).ConfigureAwait(false);
            if (info is null) return;

            var target = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "PCLPro.exe"));
            if (!File.Exists(target))
            {
                Common.Log("[ProUpdate] 当前不是 bootstrapper 启动，跳过自动替换");
                return;
            }

            Common.Log($"[ProUpdate] 发现新版本 {info.Build}（build {info.BuildNumber}）");
            var ok = await ProUpdateService.DownloadAndStageAsync(
                info, target, message => Common.Log(message)).ConfigureAwait(false);

            if (ok)
                HintService.Show(Hints.Info($"PCL 定制版 {info.Build} 已下载，重启启动器生效"));
        }
        catch (Exception ex)
        {
            Common.Log(ex, "[ProUpdate] 自动更新失败");
        }
    }
}
