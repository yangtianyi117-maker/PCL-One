using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.Logging;

namespace PCL.Core.Link.Bridge;

/// <summary>
/// 下载并校验 ViaProxy。只从官方 Release 下载，SHA-256 固定。
/// </summary>
public static class ViaProxyProvisioner
{
    public const string Version = "3.4.14";
    public const string OfficialUrl =
        "https://github.com/ViaVersion/ViaProxy/releases/download/v3.4.14/ViaProxy-3.4.14.jar";
    public const string Sha256 =
        "2894bbfb2f4342f8fde887af6c689313efd5b0e46a8006775eea0e21e2948ab5";
    public const long Size = 48_835_170;

    private static readonly HttpClient Http = CreateClient();

    public static string DirectoryPath =>
        Path.Combine(Paths.Data, "Bridge", "ViaProxy", Version);

    public static string JarPath => Path.Combine(DirectoryPath, "ViaProxy.jar");

    public static async Task<string> EnsureAsync(CancellationToken ct = default)
    {
        if (File.Exists(JarPath) && await MatchesAsync(JarPath).ConfigureAwait(false))
            return JarPath;

        Directory.CreateDirectory(DirectoryPath);
        var url = Environment.GetEnvironmentVariable("PCL_PRO_VIAPROXY_URL");
        if (string.IsNullOrWhiteSpace(url)) url = OfficialUrl;

        var temp = JarPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            LogWrapper.Info("ViaProxy", $"开始下载 ViaProxy {Version}: {url}");
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var target = new FileStream(
                             temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024,
                             FileOptions.Asynchronous))
            {
                await source.CopyToAsync(target, ct).ConfigureAwait(false);
            }

            if (!await MatchesAsync(temp).ConfigureAwait(false))
                throw new InvalidOperationException("ViaProxy 下载文件哈希不匹配");

            File.Move(temp, JarPath, true);
            LogWrapper.Info("ViaProxy", "ViaProxy 已下载并通过 SHA-256 校验");
            return JarPath;
        }
        finally
        {
            if (File.Exists(temp))
            {
                try { File.Delete(temp); }
                catch (Exception) { /* best effort */ }
            }
        }
    }

    private static async Task<bool> MatchesAsync(string path)
    {
        try
        {
            if (new FileInfo(path).Length != Size) return false;
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
            var hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);
            return string.Equals(Convert.ToHexString(hash), Sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PCLPro/1.0");
        return client;
    }
}
