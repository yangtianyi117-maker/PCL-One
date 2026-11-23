using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.IO.Net;
using PCL.Core.Link.Lobby;
using PCL.Core.Link.McPing;
using PCL.Core.Link.Pack;
using PCL.Core.Link.Room;
using PCL.Core.Utils;

namespace PCL;

/// <summary>
/// 游戏内 Fabric/NeoForge 按钮使用的本地桥：只监听 loopback，凭随机 token 建房。
/// 使用 TcpListener 而不是 HttpListener，避免 Windows URL ACL/句柄权限问题。
/// </summary>
public static class PclProBridgeServer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly object Lock = new();
    private static TcpListener? _listener;
    private static CancellationTokenSource? _cts;
    private static string _token = string.Empty;
    private static int _port;

    public static void Start()
    {
        lock (Lock)
        {
            if (_listener is not null) return;

            try
            {
                _token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                _cts = new CancellationTokenSource();
                _ = Task.Run(() => _AcceptLoopAsync(_cts.Token));

                // PCL 定制版 的联机页已经不强制登录；桥接建房同样不强制。
                LobbyInfoProvider.RequiresLogin = false;
                WriteBridgeFile();
                Common.Log($"[Bridge] 本地建房桥已启动：127.0.0.1:{_port}");
            }
            catch (Exception ex)
            {
                Common.Log(ex, "[Bridge] 本地建房桥启动失败");
                _listener = null;
            }
        }
    }

    public static void Stop()
    {
        lock (Lock)
        {
            try { _cts?.Cancel(); } catch (Exception) { /* best effort */ }
            try { _listener?.Stop(); } catch (Exception) { /* best effort */ }
            _listener = null;
            _cts = null;
        }
    }

    /// <summary>在游戏启动前把桥信息写到当前 MC 目录，供 mod 读取。</summary>
    public static void WriteBridgeFile()
    {
        if (_listener is null || string.IsNullOrWhiteSpace(_token)) return;
        try
        {
            var folders = new[]
            {
                InstanceList.McMcInstanceSelected?.PathIndie,
                GameFolders.mcFolderSelected,
                Paths.Data
            };
            var json = JsonSerializer.Serialize(new { port = _port, token = _token }, JsonOptions);
            foreach (var folder in folders
                         .Where(folder => !string.IsNullOrWhiteSpace(folder))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(folder!);
                File.WriteAllText(Path.Combine(folder!, "pclpro-bridge.json"), json);
            }
        }
        catch (Exception ex)
        {
            Common.Log(ex, "[Bridge] 写入 pclpro-bridge.json 失败");
        }
    }

    private static async Task _AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is not null)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Common.Log(ex, "[Bridge] 接受请求失败");
                await Task.Delay(200, ct).ConfigureAwait(false);
                continue;
            }

            _ = Task.Run(() => _HandleAsync(client, ct), ct);
        }
    }

    private static async Task _HandleAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            try
            {
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);

                var requestLine = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(requestLine))
                    return;

                var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    return;

                var method = parts[0];
                var path = parts[1];
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                while (true)
                {
                    var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(line)) break;
                    var index = line.IndexOf(':');
                    if (index <= 0) continue;
                    headers[line[..index].Trim()] = line[(index + 1)..].Trim();
                }

                var contentLength = headers.TryGetValue("Content-Length", out var lengthText) &&
                                    int.TryParse(lengthText, out var length)
                    ? Math.Clamp(length, 0, 64 * 1024)
                    : 0;
                var body = string.Empty;
                if (contentLength > 0)
                {
                    var buffer = new char[contentLength];
                    var read = 0;
                    while (read < contentLength)
                    {
                        var count = await reader.ReadAsync(buffer.AsMemory(read, contentLength - read), ct)
                            .ConfigureAwait(false);
                        if (count == 0) break;
                        read += count;
                    }
                    body = new string(buffer, 0, read);
                }

                if (!headers.TryGetValue("X-PCL-Token", out var token) || token != _token)
                {
                    await WriteAsync(stream, 403, new BridgeResult(false, null, "token 无效")).ConfigureAwait(false);
                    return;
                }

                if (path == "/v1/ping")
                {
                    await WriteAsync(stream, 200, new BridgeResult(true, null, null, "pclpro-bridge")).ConfigureAwait(false);
                    return;
                }

                if (path == "/v1/create" && method == "POST")
                {
                    int? requestedPort = null;
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("port", out var portElement) &&
                            portElement.TryGetInt32(out var parsedPort))
                            requestedPort = parsedPort;
                    }

                    var result = await CreateRoomAsync(requestedPort).ConfigureAwait(false);
                    await WriteAsync(stream, result.Ok ? 200 : 409, result).ConfigureAwait(false);
                    return;
                }

                await WriteAsync(stream, 404, new BridgeResult(false, null, "not found")).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Common.Log(ex, "[Bridge] 处理请求失败");
            }
        }
    }

    public static async Task<BridgeResult> CreateRoomAsync(int? requestedPort = null)
    {
        if (LobbyService.CurrentState is not LobbyState.Initialized and not LobbyState.Idle)
            return new BridgeResult(false, null, "PCL 当前已在房间中");

        LobbyService.PreferredProtocolVersion = McProtocolVersionTable.Resolve(
            InstanceList.McMcInstanceSelected?.Info.VanillaName);
        for (var attempt = 0; attempt < 3 && LobbyService.DiscoveredWorlds.Count == 0; attempt++)
        {
            await LobbyService.DiscoverWorldAsync().ConfigureAwait(false);
            await Task.Delay(2500).ConfigureAwait(false);
        }

        var world = requestedPort is { } port
            ? LobbyService.DiscoveredWorlds.FirstOrDefault(w => w.Port == port)
            : LobbyService.DiscoveredWorlds.FirstOrDefault();
        if (world is null)
            return new BridgeResult(false, null, "没有发现局域网世界，请先在游戏里对局域网开放");

        var instance = InstanceList.McMcInstanceSelected;
        PackManifest? manifest = null;
        if (instance is not null)
        {
            try
            {
                using var modrinth = new ModrinthPackSourceResolver();
                var resolver = new CompositePackSourceResolver(modrinth, new PrivatePackSourceResolver());
                var key = PackHostKeyStore.GetOrCreate();
                manifest = await PackManifestFactory
                    .BuildAsync(instance, resolver, "PCL 定制版", PackManifestSigner.PublicKeyBase64(key))
                    .ConfigureAwait(false);
                manifest = PackManifestSigner.Sign(manifest, key);
            }
            catch (Exception ex)
            {
                Common.Log(ex, "[Bridge] 生成房间清单失败，将按普通公开房创建");
            }
        }
        else
        {
            Common.Log("[Bridge] 没有选中实例，按无清单公开房创建");
        }

        var username = LobbyInfoProvider.GetUsername();
        if (string.IsNullOrWhiteSpace(username)) username = "PCL 定制版";
        var ok = await LobbyService.CreateLobbyAsync(
            world.Port, username, manifest, new RoomPublishOptions(true, "", []))
            .ConfigureAwait(false);

        return ok
            ? new BridgeResult(true, LobbyService.CurrentLobbyCode, null)
            : new BridgeResult(false, null, "创建大厅失败，请查看 PCL 日志");
    }

    private static async Task WriteAsync(Stream stream, int status, BridgeResult body)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(body, JsonOptions);
        var header = $"HTTP/1.1 {status} {(status == 200 ? "OK" : "Error")}\r\n" +
                     "Content-Type: application/json; charset=utf-8\r\n" +
                     $"Content-Length: {bytes.Length}\r\n" +
                     "Connection: close\r\n\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes).ConfigureAwait(false);
        await stream.WriteAsync(bytes).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }

    public sealed record BridgeResult(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("service")] string? Service = null);
}
