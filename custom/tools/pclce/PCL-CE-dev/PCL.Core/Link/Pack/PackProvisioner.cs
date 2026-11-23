using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link.Pack;

/// <summary>
/// 安装一个受管实例所需的输入。
/// </summary>
public sealed record PackInstallRequest(
    string InstanceName,
    string InstancePath,
    string MinecraftVersion,
    string Loader,
    string LoaderVersion);

/// <summary>
/// 由宿主实现：调用 PCL 现有的 McInstall 管线创建实例。
/// </summary>
public interface IPackInstanceInstaller
{
    Task<bool> EnsureInstanceAsync(PackInstallRequest request, CancellationToken ct = default);
}

/// <summary>
/// 由宿主实现：按 URL 打开文件流。公开源走 CDN，私有源走房主 blob。
/// </summary>
public interface IPackFileFetcher
{
    Task<Stream> OpenReadAsync(string url, CancellationToken ct = default);
}

/// <summary>
/// 把清单中的 mod 映射成可下载 URL。没有 URL 表示该来源暂不支持自动获取。
/// </summary>
public interface IPackModUrlResolver
{
    string? ResolveUrl(PackMod mod);
}

/// <summary>
/// 默认只使用清单里的 blob 字段。
/// </summary>
public sealed class BlobOnlyPackModUrlResolver : IPackModUrlResolver
{
    public string? ResolveUrl(PackMod mod) => mod.BlobUrl;
}

public sealed record PackProvisionResult(bool Success, string InstancePath, string? Error)
{
    public static PackProvisionResult Ok(string instancePath) => new(true, instancePath, null);
    public static PackProvisionResult Fail(string instancePath, string error) => new(false, instancePath, error);
}

/// <summary>
/// 按清单把 R 物化成一个受管实例：装 MC/加载器、下 mod、写配置、写标记。
/// </summary>
public sealed class PackProvisioner(
    IPackInstanceInstaller instanceInstaller,
    IPackFileFetcher fileFetcher,
    IPackModUrlResolver modUrlResolver,
    string versionsRoot,
    PackCache? cache = null)
{
    private const string MarkerFileName = "pclpro.json";

    public string GetInstanceName(PackManifest manifest)
    {
        var hash = manifest.PackHash ?? string.Empty;
        var shortHash = hash.Length >= 8 ? hash[..8] : hash;
        return string.IsNullOrEmpty(shortHash) ? "PCLPro" : $"PCLPro-{shortHash}";
    }

    public async Task<PackProvisionResult> EnsureAsync(PackManifest manifest, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var instanceName = GetInstanceName(manifest);
        var instancePath = Path.Combine(versionsRoot, instanceName);

        if (!PackManifestVerifier.VerifyHash(manifest))
            return PackProvisionResult.Fail(instancePath, "清单哈希校验失败");

        if (await _IsAlreadyProvisionedAsync(instancePath, manifest.PackHash, ct).ConfigureAwait(false))
            return PackProvisionResult.Ok(instancePath);

        try
        {
            Directory.CreateDirectory(instancePath);

            var installed = await instanceInstaller.EnsureInstanceAsync(new PackInstallRequest(
                instanceName,
                instancePath,
                manifest.MinecraftVersion,
                manifest.Loader,
                manifest.LoaderVersion), ct).ConfigureAwait(false);

            if (!installed)
                return PackProvisionResult.Fail(instancePath, "Minecraft / 加载器安装失败");

            await _InstallModsAsync(manifest, instancePath, ct).ConfigureAwait(false);
            await _InstallConfigsAsync(manifest, instancePath, ct).ConfigureAwait(false);
            await _WriteMarkerAsync(instancePath, manifest, ct).ConfigureAwait(false);

            return PackProvisionResult.Ok(instancePath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return PackProvisionResult.Fail(instancePath, ex.Message);
        }
    }

    private async Task<bool> _IsAlreadyProvisionedAsync(string instancePath, string packHash, CancellationToken ct)
    {
        var markerPath = Path.Combine(instancePath, MarkerFileName);
        if (!File.Exists(markerPath))
            return false;

        var instanceJsonPath = Path.Combine(
            instancePath, Path.GetFileName(instancePath.TrimEnd(Path.DirectorySeparatorChar)) + ".json");
        if (!File.Exists(instanceJsonPath))
            return false;

        try
        {
            var json = await File.ReadAllTextAsync(markerPath, ct).ConfigureAwait(false);
            var marker = JsonSerializer.Deserialize<PclProInstanceMarker>(json, PackJson.Options);
            return marker is not null &&
                   string.Equals(marker.PackHash, packHash, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task _InstallModsAsync(PackManifest manifest, string instancePath, CancellationToken ct)
    {
        if (manifest.Mods.Count == 0)
            return;

        var modsDirectory = Path.Combine(instancePath, "mods");
        Directory.CreateDirectory(modsDirectory);

        foreach (var mod in manifest.Mods)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(mod.Sha256))
                throw new InvalidOperationException($"mod {mod.FileName} 缺少 sha256");

            var fileName = Path.GetFileName(mod.FileName);
            if (string.IsNullOrWhiteSpace(fileName))
                throw new InvalidOperationException("mod 文件名无效");

            var targetPath = Path.Combine(modsDirectory, fileName);
            if (File.Exists(targetPath) && await _HashMatchesAsync(targetPath, mod.Sha256, ct).ConfigureAwait(false))
                continue;

            var url = modUrlResolver.ResolveUrl(mod);
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException($"无法自动获取 mod：{fileName}（来源 {mod.Source}）");

            await _DownloadToAsync(url, targetPath, mod.Sha256, ct).ConfigureAwait(false);
        }
    }

    private async Task _InstallConfigsAsync(PackManifest manifest, string instancePath, CancellationToken ct)
    {
        if (manifest.Configs.Count == 0)
            return;

        var root = Path.GetFullPath(instancePath);
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (var config in manifest.Configs)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(config.Path) || string.IsNullOrWhiteSpace(config.BlobUrl))
                continue;

            var relative = config.Path.Replace('/', Path.DirectorySeparatorChar);
            var targetPath = Path.GetFullPath(Path.Combine(root, relative));
            if (!targetPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"配置路径越界：{config.Path}");

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await _DownloadToAsync(config.BlobUrl, targetPath, config.Sha256, ct).ConfigureAwait(false);
        }
    }

    private async Task _DownloadToAsync(string url, string targetPath, string expectedSha256, CancellationToken ct)
    {
        if (cache is not null && !string.IsNullOrWhiteSpace(expectedSha256))
        {
            var cached = await cache.GetOrAddAsync(
                expectedSha256,
                Path.GetFileName(targetPath),
                token => fileFetcher.OpenReadAsync(url, token),
                ct).ConfigureAwait(false);
            PackCache.LinkOrCopy(cached, targetPath);
            return;
        }

        var tempPath = targetPath + ".pclpro-tmp";
        try
        {
            await using (var source = await fileFetcher.OpenReadAsync(url, ct).ConfigureAwait(false))
            await using (var target = new FileStream(
                             tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024,
                             FileOptions.Asynchronous))
            {
                await source.CopyToAsync(target, ct).ConfigureAwait(false);
            }

            if (!await _HashMatchesAsync(tempPath, expectedSha256, ct).ConfigureAwait(false))
                throw new InvalidOperationException($"文件哈希不匹配：{Path.GetFileName(targetPath)}");

            File.Move(tempPath, targetPath, true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch (Exception) { /* best effort */ }
            }
        }
    }

    private static async Task<bool> _HashMatchesAsync(string filePath, string expectedSha256, CancellationToken ct)
    {
        if (!File.Exists(filePath) || string.IsNullOrWhiteSpace(expectedSha256))
            return false;

        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return string.Equals(
            Convert.ToHexString(hash), expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task _WriteMarkerAsync(
        string instancePath, PackManifest manifest, CancellationToken ct)
    {
        var marker = new PclProInstanceMarker
        {
            PackHash = manifest.PackHash,
            MinecraftVersion = manifest.MinecraftVersion,
            Loader = manifest.Loader,
            LoaderVersion = manifest.LoaderVersion,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(marker, PackJson.Options);
        await File.WriteAllBytesAsync(Path.Combine(instancePath, MarkerFileName), json, ct).ConfigureAwait(false);
    }

    private sealed record PclProInstanceMarker
    {
        public string PackHash { get; init; } = string.Empty;
        public string MinecraftVersion { get; init; } = string.Empty;
        public string Loader { get; init; } = string.Empty;
        public string LoaderVersion { get; init; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; init; }
    }
}
