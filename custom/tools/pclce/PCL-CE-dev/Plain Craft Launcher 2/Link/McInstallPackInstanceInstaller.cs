using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Link.Pack;

namespace PCL;

/// <summary>WPF 侧的实例安装器：把 PackInstallRequest 转成 DownloadUi 的 McInstallRequest。</summary>
public sealed class McInstallPackInstanceInstaller : IPackInstanceInstaller
{
    public async Task<bool> EnsureInstanceAsync(
        PackInstallRequest request, CancellationToken ct = default)
    {
        var mcRequest = new McInstallRequest
        {
            minecraftName = request.MinecraftVersion,
            targetInstanceName = request.InstanceName,
            targetInstanceFolder = request.InstancePath.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar
        };

        switch (request.Loader.ToLowerInvariant())
        {
            case "neoforge":
                mcRequest.neoForgeVersion = request.LoaderVersion;
                break;
            case "forge":
                mcRequest.forgeVersion = request.LoaderVersion;
                break;
            case "fabric":
                mcRequest.fabricVersion = request.LoaderVersion;
                break;
            case "vanilla":
                break;
            default:
                // Quilt / LegacyFabric 等暂不在自动安装范围。
                return false;
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            return await dispatcher
                .InvokeAsync(() => DownloadUi.McInstallAsync(mcRequest, "零整合包", ct))
                .Task.Unwrap()
                .ConfigureAwait(false);
        }

        return await DownloadUi.McInstallAsync(mcRequest, "零整合包", ct).ConfigureAwait(false);
    }
}
