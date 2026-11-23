using System.Text.Json.Serialization;

namespace PCL.Link.Protocol;

/// <summary>
/// 公开房间索引中的一条房间记录。私有房间不会进入索引。
/// </summary>
public sealed record RoomRecord
{
    public const int CurrentSchema = 1;

    [JsonPropertyName("schema")]
    public int Schema { get; init; } = CurrentSchema;

    [JsonPropertyName("roomId")]
    public string RoomId { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("mc")]
    public string MinecraftVersion { get; init; } = string.Empty;

    [JsonPropertyName("loader")]
    public string Loader { get; init; } = string.Empty;

    [JsonPropertyName("loaderVersion")]
    public string LoaderVersion { get; init; } = string.Empty;

    [JsonPropertyName("packHash")]
    public string PackHash { get; init; } = string.Empty;

    [JsonPropertyName("players")]
    public int Players { get; init; }

    [JsonPropertyName("maxPlayers")]
    public int MaxPlayers { get; init; } = 8;

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>public / private。索引只接受 public。</summary>
    [JsonPropertyName("visibility")]
    public string Visibility { get; init; } = "public";

    [JsonPropertyName("joinCode")]
    public string JoinCode { get; init; } = string.Empty;

    [JsonPropertyName("hostName")]
    public string HostName { get; init; } = string.Empty;

    [JsonPropertyName("hostPublicKey")]
    public string HostPublicKey { get; init; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.AddSeconds(90);

    [JsonPropertyName("signature")]
    public string? Signature { get; init; }

    [JsonIgnore]
    public bool IsExpired => ExpiresAt <= DateTimeOffset.UtcNow;

    [JsonIgnore]
    public bool IsPublic => string.Equals(Visibility, "public", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// 公开房间查询条件。
/// </summary>
public sealed record RoomFilter
{
    public string? MinecraftVersion { get; init; }
    public string? Loader { get; init; }
    public string? Query { get; init; }
    public int Limit { get; init; } = 50;
}
