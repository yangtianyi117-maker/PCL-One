using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link.Bedrock;

/// <summary>
/// Minecraft Bedrock (RakNet) unconnected ping.
///
/// The desktop already knows how to ping Java servers; the phone side is Bedrock, so the
/// launcher needs one small, dependency-free probe to show whether the Geyser entry is alive.
/// Request: 0x01 + time(8) + offline magic(16) + client guid(8) = 33 bytes.
/// Reply:   0x1C + time(8) + server guid(8) + offline magic(16) + string length(2) + UTF-8.
/// </summary>
public static class BedrockPing
{
    private static readonly byte[] Magic =
    [
        0x00, 0xFF, 0xFF, 0x00, 0xFE, 0xFE, 0xFE, 0xFE,
        0xFD, 0xFD, 0xFD, 0xFD, 0x12, 0x34, 0x56, 0x78
    ];

    public sealed record Result(
        string Motd,
        string Version,
        int Players,
        int MaxPlayers,
        long LatencyMs,
        string LevelName);

    public static async Task<Result?> PingAsync(string host, int port, int timeoutMs = 2500,
        CancellationToken ct = default)
    {
        return await Task.Run(() => Ping(host, port, timeoutMs), ct).ConfigureAwait(false);
    }

    public static Result? Ping(string host, int port, int timeoutMs = 2500)
    {
        UdpClient? client = null;
        try
        {
            using var timeout = new CancellationTokenSource(Math.Max(300, timeoutMs));
            client = new UdpClient();
            client.Client.ReceiveTimeout = Math.Max(300, timeoutMs);
            client.Connect(host, port);

            var request = BuildRequest();
            var start = DateTime.UtcNow;
            client.Send(request, request.Length);

            var remote = new IPEndPoint(IPAddress.Any, 0);
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(300, timeoutMs));
            while (DateTime.UtcNow < deadline && !timeout.IsCancellationRequested)
            {
                var data = client.Receive(ref remote);
                if (data.Length < 35 || data[0] != 0x1C) continue;
                var result = Parse(data);
                if (result is null) continue;
                return result with
                {
                    LatencyMs = Math.Max(1, (long)(DateTime.UtcNow - start).TotalMilliseconds)
                };
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            client?.Dispose();
        }
    }

    private static byte[] BuildRequest()
    {
        var buffer = new byte[33];
        buffer[0] = 0x01;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < 8; i++)
            buffer[1 + i] = (byte)(now >> (8 * (7 - i)));
        Magic.CopyTo(buffer, 9);
        var guid = 0x50434C50726F4D4CUL;
        for (var i = 0; i < 8; i++)
            buffer[25 + i] = (byte)(guid >> (8 * (7 - i)));
        return buffer;
    }

    private static Result? Parse(byte[] data)
    {
        try
        {
            var offset = 1 + 8 + 8 + 16;
            if (data.Length < offset + 2) return null;
            var length = (data[offset] << 8) | data[offset + 1];
            offset += 2;
            if (length <= 0 || data.Length < offset + length) return null;

            var payload = Encoding.UTF8.GetString(data, offset, length);
            var fields = payload.Split(';');
            var start = fields.Length > 1 && fields[0].Trim().Equals("MCPE", StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0;

            string Field(int index) => fields.Length > index ? Strip(fields[index]) : "";
            int Number(int index) => int.TryParse(Field(index), out var value) ? value : -1;

            var motd = Field(start);
            var version = Field(start + 2);
            var players = Number(start + 3);
            var maxPlayers = Number(start + 4);
            var level = Field(start + 6);

            return new Result(motd, version, players, maxPlayers, 0, level);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Strip(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        // Bedrock MOTD uses § colour codes; strip them for the launcher UI.
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '§' && i + 1 < value.Length)
            {
                i++;
                continue;
            }

            builder.Append(value[i]);
        }

        return builder.ToString().Trim();
    }
}
