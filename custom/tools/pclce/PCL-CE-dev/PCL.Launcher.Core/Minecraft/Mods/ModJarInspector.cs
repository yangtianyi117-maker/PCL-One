using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PCL;

/// <summary>
/// 读取一个 mod jar 的元数据：加载器、模组 ID、版本、MC 版本约束、依赖。
/// 这是“把 jar 拖进来就能安装”的第一层：不执行游戏代码，只读元数据。
/// </summary>
public sealed record ModJarInfo
{
    public required string Path { get; init; }
    public string Loader { get; init; } = "unknown";
    public string ModId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string? MinecraftConstraint { get; init; }
    public IReadOnlyList<ModDependency> Dependencies { get; init; } = [];
    public string? Sha256 { get; init; }
    public bool IsClientOnly { get; init; }

    public bool IsCompatible(string minecraftVersion) =>
        string.IsNullOrWhiteSpace(MinecraftConstraint) ||
        McConstraintMatcher.Satisfies(MinecraftConstraint, Loader, minecraftVersion);
}

public sealed record ModDependency(string ModId, string? VersionRange, bool Optional, string Loader);

public static class ModJarInspector
{
    public static async Task<ModJarInfo> InspectAsync(string jarPath, CancellationToken ct = default)
    {
        await using var stream = new FileStream(jarPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var sha = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        var path = Convert.ToHexString(sha).ToLowerInvariant();
        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, true);

        var fabric = zip.GetEntry("fabric.mod.json");
        if (fabric is not null) return await ReadFabricAsync(jarPath, path, fabric, ct).ConfigureAwait(false);

        var quilt = zip.GetEntry("quilt.mod.json");
        if (quilt is not null) return await ReadQuiltAsync(jarPath, path, quilt, ct).ConfigureAwait(false);

        var neo = zip.GetEntry("META-INF/neoforge.mods.toml");
        if (neo is not null) return await ReadForgeTomlAsync(jarPath, path, neo, "NeoForge", ct).ConfigureAwait(false);

        var forge = zip.GetEntry("META-INF/mods.toml");
        if (forge is not null) return await ReadForgeTomlAsync(jarPath, path, forge, "Forge", ct).ConfigureAwait(false);

        var mcmod = zip.GetEntry("mcmod.info");
        if (mcmod is not null) return await ReadMcmodAsync(jarPath, path, mcmod, ct).ConfigureAwait(false);

        return new ModJarInfo { Path = jarPath, Sha256 = path };
    }

    private static async Task<ModJarInfo> ReadFabricAsync(
        string jarPath, string sha, ZipArchiveEntry entry, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await ReadEntryAsync(entry, ct).ConfigureAwait(false));
        var root = doc.RootElement;
        var depends = root.TryGetProperty("depends", out var dep) ? dep : default;
        var dependencies = new List<ModDependency>();
        if (depends.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in depends.EnumerateObject())
            {
                if (property.Name is "minecraft" or "fabricloader" or "java") continue;
                dependencies.Add(new ModDependency(
                    property.Name, property.Value.ToString(), false, "Fabric"));
            }
        }

        return new ModJarInfo
        {
            Path = jarPath,
            Loader = "Fabric",
            ModId = root.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
            Name = root.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
            Version = root.TryGetProperty("version", out var version) ? version.GetString() ?? "" : "",
            MinecraftConstraint = depends.ValueKind == JsonValueKind.Object &&
                                  depends.TryGetProperty("minecraft", out var mc)
                ? mc.ToString()
                : null,
            Dependencies = dependencies,
            Sha256 = sha,
            IsClientOnly = root.TryGetProperty("environment", out var env) &&
                           string.Equals(env.GetString(), "client", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static async Task<ModJarInfo> ReadQuiltAsync(
        string jarPath, string sha, ZipArchiveEntry entry, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await ReadEntryAsync(entry, ct).ConfigureAwait(false));
        var root = doc.RootElement;
        var loader = root.TryGetProperty("quilt_loader", out var ql) ? ql : root;
        var metadata = loader.TryGetProperty("metadata", out var meta) ? meta : default;
        return new ModJarInfo
        {
            Path = jarPath,
            Loader = "Quilt",
            ModId = loader.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
            Name = metadata.ValueKind == JsonValueKind.Object &&
                   metadata.TryGetProperty("name", out var name)
                ? name.GetString() ?? ""
                : "",
            Version = loader.TryGetProperty("version", out var version) ? version.GetString() ?? "" : "",
            Sha256 = sha
        };
    }

    private static async Task<ModJarInfo> ReadForgeTomlAsync(
        string jarPath, string sha, ZipArchiveEntry entry, string loader, CancellationToken ct)
    {
        var text = await ReadEntryAsync(entry, ct).ConfigureAwait(false);
        var modId = RegexValue(text, "modId") ?? "";
        var version = RegexValue(text, "version") ?? "";
        var name = RegexValue(text, "displayName") ?? modId;
        var mcRange = ExtractDependencyRange(text, "minecraft");
        var deps = new List<ModDependency>();
        foreach (var block in text.Split("[[dependencies", StringSplitOptions.RemoveEmptyEntries).Skip(1))
        {
            var depId = RegexValue(block, "modId");
            if (string.IsNullOrWhiteSpace(depId) || depId is "minecraft" or "neoforge" or "forge") continue;
            deps.Add(new ModDependency(
                depId,
                RegexValue(block, "versionRange"),
                !string.Equals(RegexValue(block, "type"), "required", StringComparison.OrdinalIgnoreCase),
                loader));
        }

        return new ModJarInfo
        {
            Path = jarPath,
            Loader = loader,
            ModId = modId,
            Name = name,
            Version = version,
            MinecraftConstraint = mcRange,
            Dependencies = deps,
            Sha256 = sha
        };
    }

    private static async Task<ModJarInfo> ReadMcmodAsync(
        string jarPath, string sha, ZipArchiveEntry entry, CancellationToken ct)
    {
        var text = await ReadEntryAsync(entry, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0
            ? doc.RootElement[0]
            : doc.RootElement;
        return new ModJarInfo
        {
            Path = jarPath,
            Loader = "Forge",
            ModId = root.TryGetProperty("modid", out var id) ? id.GetString() ?? "" : "",
            Name = root.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
            Version = root.TryGetProperty("version", out var version) ? version.GetString() ?? "" : "",
            Sha256 = sha
        };
    }

    private static async Task<string> ReadEntryAsync(ZipArchiveEntry entry, CancellationToken ct)
    {
        await using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(ct).ConfigureAwait(false);
    }

    private static string? RegexValue(string text, string key)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            text, $"(?m)^\\s*{System.Text.RegularExpressions.Regex.Escape(key)}\\s*=\\s*\"([^\"]*)\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractDependencyRange(string text, string modId)
    {
        foreach (var block in text.Split("[[dependencies", StringSplitOptions.RemoveEmptyEntries).Skip(1))
        {
            if (string.Equals(RegexValue(block, "modId"), modId, StringComparison.OrdinalIgnoreCase))
                return RegexValue(block, "versionRange");
        }

        return null;
    }
}
