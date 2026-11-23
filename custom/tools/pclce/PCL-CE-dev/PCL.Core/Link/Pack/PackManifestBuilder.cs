using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link.Pack;

/// <summary>
/// 扫描一个受管实例的 mods/configs，生成可分发的内容清单。
/// </summary>
public sealed class PackManifestBuilder(IPackSourceResolver sourceResolver)
{
    private static readonly string[] _ConfigDirectories = ["config", "defaultconfigs"];

    public async Task<PackManifest> BuildAsync(PackBuildOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Directory.Exists(options.InstancePath))
            throw new DirectoryNotFoundException($"Instance directory not found: {options.InstancePath}");

        var mods = await _CollectModsAsync(options, ct).ConfigureAwait(false);
        var configs = options.IncludeConfigs
            ? await _CollectConfigsAsync(options, ct).ConfigureAwait(false)
            : [];

        var manifest = new PackManifest
        {
            Schema = PackConstants.SchemaVersion,
            MinecraftVersion = options.MinecraftVersion,
            Loader = options.Loader,
            LoaderVersion = options.LoaderVersion,
            Mods = mods,
            Configs = configs,
            Host = string.IsNullOrWhiteSpace(options.HostName) && string.IsNullOrWhiteSpace(options.HostPublicKey)
                ? null
                : new PackHost
                {
                    Name = options.HostName,
                    PublicKey = options.HostPublicKey
                }
        };

        return manifest with { PackHash = PackManifestHasher.ComputeHash(manifest) };
    }

    private async Task<List<PackMod>> _CollectModsAsync(PackBuildOptions options, CancellationToken ct)
    {
        var modsDirectory = Path.Combine(options.InstancePath, "mods");
        if (!Directory.Exists(modsDirectory))
            return [];

        var result = new List<PackMod>();
        foreach (var filePath in Directory.EnumerateFiles(modsDirectory, "*.jar", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();

            var sha256 = await _HashFileAsync(filePath, ct).ConfigureAwait(false);
            var source = await sourceResolver.ResolveAsync(filePath, sha256, ct).ConfigureAwait(false)
                         ?? new PackSourceInfo { Source = PackConstants.SourcePrivate };

            result.Add(new PackMod
            {
                Source = source.Source,
                ProjectId = source.ProjectId,
                VersionId = source.VersionId,
                Sha256 = sha256,
                FileName = Path.GetFileName(filePath),
                Side = source.Side,
                BlobUrl = source.BlobUrl
            });
        }

        return result;
    }

    private async Task<List<PackConfig>> _CollectConfigsAsync(PackBuildOptions options, CancellationToken ct)
    {
        var result = new List<PackConfig>();

        foreach (var directoryName in _ConfigDirectories)
        {
            var directory = Path.Combine(options.InstancePath, directoryName);
            if (!Directory.Exists(directory))
                continue;

            foreach (var filePath in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();

                var fileName = Path.GetFileName(filePath);
                if (IsSensitive(fileName, options.SensitiveFileNameFragments))
                    continue;

                var info = new FileInfo(filePath);
                if (info.Length > options.MaxConfigBytes)
                    continue;

                var sha256 = await _HashFileAsync(filePath, ct).ConfigureAwait(false);
                var relativePath = Path.GetRelativePath(options.InstancePath, filePath)
                    .Replace(Path.DirectorySeparatorChar, '/');

                result.Add(new PackConfig
                {
                    Path = relativePath,
                    Sha256 = sha256
                });
            }
        }

        return result;
    }

    private static bool IsSensitive(string fileName, IReadOnlyCollection<string> fragments)
        => fragments.Any(fragment =>
            !string.IsNullOrWhiteSpace(fragment) &&
            fileName.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static async Task<string> _HashFileAsync(string filePath, CancellationToken ct)
    {
        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous);

        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
