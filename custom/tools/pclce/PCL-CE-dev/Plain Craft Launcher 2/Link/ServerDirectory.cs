using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.Logging;

namespace PCL;

/// <summary>服务器目录中的一条记录。字段与模组条目同构：名称、标签、描述、版本、人数、延迟、来源。</summary>
public sealed record ServerEntry
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string SubTitle { get; init; } = "";
    public string Description { get; init; } = "";
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string MinecraftVersion { get; init; } = "";
    public string Loader { get; init; } = "";
    public string Java { get; init; } = "";
    public string Bedrock { get; init; } = "";
    public string Source { get; init; } = "";
    public string Icon { get; init; } = "";
}

/// <summary>
/// 服务器目录：从配置的地址拉一份 JSON，失败时退回内置的固定服务器。
///
/// 第一性：联机入口的本质是「去哪台服务器」，需要的只是可描述的元数据 + 实时 ping，
/// 不需要房间、房间码或 P2P 打洞，所以它就是一个可以像模组列表一样浏览的目录。
/// </summary>
public static class ServerDirectory
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static ServerEntry Builtin => new()
    {
        Id = "official",
        Name = "原版生存",
        SubTitle = "Vanilla 26.3",
        Description = "官方固定服务器，PC 和手机都能直接进。",
        Tags = ["生存", "原版", "基岩可进"],
        MinecraftVersion = "26.3",
        Loader = "Vanilla",
        Java = Config.Link.ServerJava,
        Bedrock = Config.Link.ServerBedrock,
        Source = "PCL 定制版"
    };

    public static async Task<IReadOnlyList<ServerEntry>> LoadAsync(CancellationToken ct = default)
    {
        var fallback = new List<ServerEntry> { Builtin };
        var url = Config.Link.ServerListUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url)) return fallback;

        try
        {
            var json = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
            var list = Parse(json);
            return list.Count > 0 ? list : fallback;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("ServerDirectory", $"拉取服务器目录失败：{ex.Message}");
            return fallback;
        }
    }

    private static List<ServerEntry> Parse(string json)
    {
        var result = new List<ServerEntry>();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var array = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("servers", out var servers) && servers.ValueKind == JsonValueKind.Array
                ? servers
                : default;
        if (array.ValueKind != JsonValueKind.Array) return result;

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            result.Add(new ServerEntry
            {
                Id = Str(item, "id"),
                Name = Str(item, "name"),
                SubTitle = Str(item, "subtitle"),
                Description = Str(item, "description"),
                Tags = Tags(item),
                MinecraftVersion = Str(item, "mc"),
                Loader = Str(item, "loader"),
                Java = Str(item, "java"),
                Bedrock = Str(item, "bedrock"),
                Source = Str(item, "source"),
                Icon = Str(item, "icon")
            });
        }

        return result;
    }

    private static string Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static IReadOnlyList<string> Tags(JsonElement obj)
    {
        if (!obj.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (var tag in tags.EnumerateArray())
            if (tag.ValueKind == JsonValueKind.String)
                list.Add(tag.GetString() ?? "");
        return list;
    }
}
