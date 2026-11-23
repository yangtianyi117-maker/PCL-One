using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.Link.Pack;

namespace PCL;

/// <summary>
/// 加入者侧的自动准备入口：把房间清单物化成受管实例。
///
/// 业务逻辑放在 PCL.Launcher.Core；实例安装器由 UI 注入（WPF 的 DownloadUi），
/// 避免 Core 反向依赖 app。
/// </summary>
public static class PackJoinService
{
    public static async Task<PackProvisionResult> EnsureAsync(
        PackManifest manifest,
        IPackInstanceInstaller instanceInstaller,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(manifest.Signature) &&
            !PackManifestSigner.VerifySignature(manifest))
            return PackProvisionResult.Fail(string.Empty, "清单签名无效，已拒绝安装");

        if (string.IsNullOrWhiteSpace(manifest.Signature))
            LauncherCommon.Log("[Link] 房间清单未签名，按兼容模式继续");

        var versionsRoot = Path.Combine(GameFolders.mcFolderSelected, "versions");
        var cache = new PackCache(
            Path.Combine(Paths.Data, "PackCache"),
            Math.Max(1, Config.Link.PackCacheLimitMb) * 1024L * 1024L);
        var provisioner = new PackProvisioner(
            instanceInstaller,
            new HttpClientPackFileFetcher(),
            new BlobOnlyPackModUrlResolver(),
            versionsRoot,
            cache);

        return await provisioner.EnsureAsync(manifest, cancellationToken).ConfigureAwait(false);
    }

    private sealed class HttpClientPackFileFetcher : IPackFileFetcher
    {
        private static readonly HttpClient Client = _CreateClient();

        public async Task<Stream> OpenReadAsync(string url, CancellationToken ct = default)
        {
            using var response = await Client
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            return new MemoryStream(bytes, writable: false);
        }

        private static HttpClient _CreateClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PCLPro", "1.0"));
            return client;
        }
    }
}
