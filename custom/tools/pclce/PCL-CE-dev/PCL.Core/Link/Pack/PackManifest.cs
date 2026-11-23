using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.Link.Pack;

/// <summary>
/// 零整合包联机的常量。
/// </summary>
public static class PackConstants
{
    /// <summary>清单结构版本。</summary>
    public const int SchemaVersion = 1;

    /// <summary>Scaffolding 请求类型。</summary>
    public const string ManifestRequestType = "c:pack_manifest";

    public const string SourceModrinth = "modrinth";
    public const string SourceCurseForge = "curseforge";
    public const string SourcePrivate = "private";

    public const string SideBoth = "both";
    public const string SideClient = "client";
    public const string SideServer = "server";
}

/// <summary>
/// 房间的内容清单。它描述“要跑什么”，不包含 mod 文件本身。
/// </summary>
public sealed record PackManifest
{
    [JsonPropertyName("schema")]
    public int Schema { get; init; } = PackConstants.SchemaVersion;

    /// <summary>清单哈希（小写十六进制 SHA-256），不包含本字段与签名。</summary>
    [JsonPropertyName("packHash")]
    public string PackHash { get; init; } = string.Empty;

    [JsonPropertyName("mc")]
    public string MinecraftVersion { get; init; } = string.Empty;

    [JsonPropertyName("loader")]
    public string Loader { get; init; } = string.Empty;

    [JsonPropertyName("loaderVersion")]
    public string LoaderVersion { get; init; } = string.Empty;

    [JsonPropertyName("mods")]
    public IReadOnlyList<PackMod> Mods { get; init; } = [];

    [JsonPropertyName("configs")]
    public IReadOnlyList<PackConfig> Configs { get; init; } = [];

    [JsonPropertyName("host")]
    public PackHost? Host { get; init; }

    [JsonPropertyName("signature")]
    public string? Signature { get; init; }

    [JsonIgnore]
    public bool HasContent => Mods.Count > 0 || Configs.Count > 0;

    [JsonIgnore]
    public bool IsEmpty => string.IsNullOrWhiteSpace(MinecraftVersion) && !HasContent;
}

/// <summary>
/// 清单中的一个 mod。公开源只记录 project/version，私有源必须提供 blob。
/// </summary>
public sealed record PackMod
{
    [JsonPropertyName("source")]
    public string Source { get; init; } = PackConstants.SourcePrivate;

    [JsonPropertyName("projectId")]
    public string? ProjectId { get; init; }

    [JsonPropertyName("versionId")]
    public string? VersionId { get; init; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = string.Empty;

    /// <summary>client / server / both。</summary>
    [JsonPropertyName("side")]
    public string Side { get; init; } = PackConstants.SideBoth;

    /// <summary>私有 mod 或需要房主托管的文件地址。</summary>
    [JsonPropertyName("blob")]
    public string? BlobUrl { get; init; }
}

/// <summary>
/// 清单中的一个配置文件。没有 blob 时客户端只校验存在性，不主动写入。
/// </summary>
public sealed record PackConfig
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;

    [JsonPropertyName("blob")]
    public string? BlobUrl { get; init; }
}

public sealed record PackHost
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("publicKey")]
    public string? PublicKey { get; init; }
}

/// <summary>
/// 构建清单时的输入。实例路径只用于扫描 mods/configs。
/// </summary>
public sealed record PackBuildOptions
{
    public required string InstancePath { get; init; }
    public required string MinecraftVersion { get; init; }
    public required string Loader { get; init; }
    public required string LoaderVersion { get; init; }

    public string? HostName { get; init; }
    public string? HostPublicKey { get; init; }

    public bool IncludeConfigs { get; init; } = true;

    /// <summary>单个配置文件的大小上限，默认 1 MiB。</summary>
    public long MaxConfigBytes { get; init; } = 1024 * 1024;

    /// <summary>文件名包含这些片段时默认不进入清单。</summary>
    public IReadOnlyCollection<string> SensitiveFileNameFragments { get; init; } =
    [
        "token", "secret", "password", "passwd", "credential", "apikey", "api_key"
    ];
}
