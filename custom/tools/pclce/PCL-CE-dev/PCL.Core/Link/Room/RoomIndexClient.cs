using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Link.Protocol;

namespace PCL.Core.Link.Room;

/// <summary>
/// 公开房索引 HTTP 客户端。索引不可用时只影响公开房，不影响口令房。
/// </summary>
public sealed class RoomIndexClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;

    public RoomIndexClient(string baseUrl, HttpClient? http = null)
    {
        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var uri))
            throw new ArgumentException("公开房索引地址无效", nameof(baseUrl));

        _http = http ?? new HttpClient();
        _http.BaseAddress = uri;
        _http.Timeout = TimeSpan.FromSeconds(5);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PCLPro/1.0");
    }

    public static string ConfiguredUrl =>
        Environment.GetEnvironmentVariable("PCL_PRO_ROOM_INDEX_URL") is { Length: > 0 } envUrl
            ? envUrl
            : Config.Link.RoomIndexUrl;

    public static bool IsConfigured =>
        Config.Link.PublicRoomsEnabled &&
        !string.IsNullOrWhiteSpace(ConfiguredUrl);

    public static RoomIndexClient? TryCreate()
    {
        if (!IsConfigured) return null;
        return new RoomIndexClient(ConfiguredUrl);
    }

    public async Task PublishAsync(RoomRecord record, CancellationToken ct = default)
    {
        using var response = await _http.PutAsJsonAsync(
            $"v1/rooms/{Uri.EscapeDataString(record.RoomId)}", record, JsonOptions, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<RoomRecord>> SearchAsync(RoomFilter filter, CancellationToken ct = default)
    {
        var query = new List<string> { $"limit={Math.Clamp(filter.Limit, 1, 100)}" };
        if (!string.IsNullOrWhiteSpace(filter.MinecraftVersion))
            query.Add("mc=" + Uri.EscapeDataString(filter.MinecraftVersion));
        if (!string.IsNullOrWhiteSpace(filter.Loader))
            query.Add("loader=" + Uri.EscapeDataString(filter.Loader));
        if (!string.IsNullOrWhiteSpace(filter.Query))
            query.Add("q=" + Uri.EscapeDataString(filter.Query));

        return await _http.GetFromJsonAsync<List<RoomRecord>>(
                   "v1/rooms?" + string.Join('&', query), JsonOptions, ct)
               .ConfigureAwait(false) ?? [];
    }

    public async Task DeleteAsync(string roomId, System.Security.Cryptography.ECDsa key, CancellationToken ct = default)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var signature = RoomSignature.SignDelete(roomId, ts, key);
        var url = $"v1/rooms/{Uri.EscapeDataString(roomId)}" +
                  $"?ts={ts}&key={Uri.EscapeDataString(publicKey)}&sig={Uri.EscapeDataString(signature)}";
        using var response = await _http.DeleteAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task ReportAsync(string roomId, string reason, CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync(
            $"v1/rooms/{Uri.EscapeDataString(roomId)}/report",
            new { reason }, JsonOptions, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => _http.Dispose();
}
