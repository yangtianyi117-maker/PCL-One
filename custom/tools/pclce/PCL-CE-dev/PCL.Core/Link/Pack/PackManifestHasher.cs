using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PCL.Core.Link.Pack;

/// <summary>
/// 对清单做确定性哈希。使用长度前缀的二进制编码，避免 JSON 字段顺序/空白影响。
/// </summary>
public static class PackManifestHasher
{
    private const string Domain = "PCLPro.PackManifest";

    public static string ComputeHash(PackManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            WriteString(writer, Domain);
            writer.Write(manifest.Schema);
            WriteString(writer, manifest.MinecraftVersion);
            WriteString(writer, manifest.Loader);
            WriteString(writer, manifest.LoaderVersion);
            WriteString(writer, manifest.Host?.PublicKey);

            var mods = manifest.Mods
                .OrderBy(mod => mod.Source, StringComparer.Ordinal)
                .ThenBy(mod => mod.ProjectId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(mod => mod.VersionId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(mod => mod.Sha256, StringComparer.Ordinal)
                .ThenBy(mod => mod.FileName, StringComparer.Ordinal)
                .ToList();

            writer.Write(mods.Count);
            foreach (var mod in mods)
            {
                WriteString(writer, mod.Source);
                WriteString(writer, mod.ProjectId);
                WriteString(writer, mod.VersionId);
                WriteString(writer, mod.Sha256);
                WriteString(writer, mod.FileName);
                WriteString(writer, mod.Side);
                WriteString(writer, mod.BlobUrl);
            }

            var configs = manifest.Configs
                .OrderBy(config => config.Path, StringComparer.Ordinal)
                .ToList();

            writer.Write(configs.Count);
            foreach (var config in configs)
            {
                WriteString(writer, config.Path);
                WriteString(writer, config.Sha256);
                WriteString(writer, config.BlobUrl);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteString(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}

public static class PackManifestVerifier
{
    /// <summary>
    /// 校验清单自带哈希是否与内容一致。签名校验在后续阶段接入。
    /// </summary>
    public static bool VerifyHash(PackManifest manifest)
    {
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.PackHash))
            return false;

        var expected = PackManifestHasher.ComputeHash(manifest);
        return string.Equals(expected, manifest.PackHash, StringComparison.OrdinalIgnoreCase);
    }
}
