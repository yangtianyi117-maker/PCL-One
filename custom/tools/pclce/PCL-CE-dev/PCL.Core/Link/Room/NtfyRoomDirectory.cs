using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCL.Link.Protocol;

namespace PCL.Core.Link.Room;

/// <summary>
/// 零配置公开房目录：用 ntfy.sh 的公共 topic 做广播板。
/// 消息公开可见，房间记录带签名；客户端只接受验签通过且未过期的记录。
/// </summary>
public sealed class NtfyRoomDirectory : IRoomDirectory
{
    public const string DefaultTopic = "pclpro-rooms-v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly string _topic;

    public NtfyRoomDirectory(string? topic = null, HttpClient? http = null)
    {
        _topic = string.IsNullOrWhiteSpace(topic) ? DefaultTopic : topic.Trim();
        _http = http ?? new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(8);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PCLPro/1.0");
    }

    public async Task PublishAsync(RoomRecord record, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(record, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "text/plain");
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"https://ntfy.sh/{Uri.EscapeDataString(_topic)}")
        {
            Content = content
        };
        request.Headers.TryAddWithoutValidation("Title", "PCLPro Room");
        request.Headers.TryAddWithoutValidation("Tags", "video_game");

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<RoomRecord>> SearchAsync(RoomFilter filter, CancellationToken ct = default)
    {
        var url = $"https://ntfy.sh/{Uri.EscapeDataString(_topic)}/json?poll=1&since=15m";
        var text = await _http.GetStringAsync(url, ct).ConfigureAwait(false);

        var latest = new Dictionary<string, RoomRecord>(StringComparer.Ordinal);
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (!root.TryGetProperty("event", out var evt) ||
                    !string.Equals(evt.GetString(), "message", StringComparison.Ordinal))
                    continue;

                if (!root.TryGetProperty("message", out var messageElement)) continue;
                var message = messageElement.GetString();
                if (string.IsNullOrWhiteSpace(message) ||
                    !message.TrimStart().StartsWith('{'))
                    continue;

                var record = JsonSerializer.Deserialize<RoomRecord>(message, JsonOptions);
                if (record is null || record.IsExpired || !record.IsPublic) continue;
                if (!RoomSignature.Verify(record)) continue;

                latest[record.RoomId] = record;
            }
            catch (JsonException)
            {
                // 忽略公共频道里的非房间消息
            }
        }

        var result = new List<RoomRecord>();
        foreach (var record in latest.Values)
        {
            if (!string.IsNullOrWhiteSpace(filter.MinecraftVersion) &&
                !string.Equals(record.MinecraftVersion, filter.MinecraftVersion, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(filter.Loader) &&
                !string.Equals(record.Loader, filter.Loader, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(filter.Query) &&
                !record.Name.Contains(filter.Query, StringComparison.OrdinalIgnoreCase) &&
                !record.HostName.Contains(filter.Query, StringComparison.OrdinalIgnoreCase))
                continue;
            result.Add(record);
        }

        result.Sort((a, b) => b.Players.CompareTo(a.Players));
        var limit = Math.Clamp(filter.Limit <= 0 ? 50 : filter.Limit, 1, 100);
        return result.GetRange(0, Math.Min(limit, result.Count));
    }

    public Task DeleteAsync(string roomId, ECDsa key, CancellationToken ct = default)
    {
        // ntfy 公共频道不做删除；房主停止心跳后 90 秒自然过期。
        return Task.CompletedTask;
    }

    public void Dispose() => _http.Dispose();
}
