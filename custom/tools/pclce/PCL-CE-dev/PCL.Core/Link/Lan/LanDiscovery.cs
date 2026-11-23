using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link.Lan;

/// <summary>
/// 加入方发现桥：往本机各网段的广播发一问一答的 UDP 探测，
/// 广播被挡（AP 隔离 / 跨 VLAN）时退化成逐地址扫描。
///
/// 发现本身不依赖任何服务器；找到房间后由 UI 决定是直接启动游戏
/// （把房主地址写进 <c>--quickPlayMultiplayer</c>）还是走口令房。
/// </summary>
public sealed class LanDiscovery
{
    /// <summary>应答器端口。</summary>
    public int ProbePort { get; init; } = LanProtocol.ResponderPort;

    /// <summary>单轮探测总超时。</summary>
    public int TimeoutMs { get; init; } = 1200;

    /// <summary>广播没人回时是否退化成逐地址扫描。</summary>
    public bool ScanFallback { get; init; } = true;

    public async Task<IReadOnlyList<LanRoom>> ProbeOnceAsync(
        IEnumerable<string>? extraSubnets = null, CancellationToken ct = default)
    {
        var rooms = new Dictionary<string, LanRoom>(StringComparer.OrdinalIgnoreCase);

        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.EnableBroadcast = true;
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

        var probe = Encoding.UTF8.GetBytes(LanProtocol.Probe);

        foreach (var target in LanProtocol.BroadcastTargets())
        {
            try
            {
                await udp.SendAsync(probe, new IPEndPoint(target, ProbePort), ct).ConfigureAwait(false);
            }
            catch
            {
                // 单个网卡不可用不影响其他网段
            }
        }

        foreach (var subnet in extraSubnets ?? [])
        {
            if (!LanProtocol.TryParseSubnet(subnet, out var broadcast, out _)) continue;
            try
            {
                await udp.SendAsync(probe, new IPEndPoint(broadcast, ProbePort), ct).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }

        await CollectAsync(udp, rooms, TimeSpan.FromMilliseconds(TimeoutMs * 0.4), ct).ConfigureAwait(false);

        if (rooms.Count == 0 && ScanFallback)
        {
            foreach (var host in LanProtocol.ScanTargets())
            {
                try
                {
                    await udp.SendAsync(probe, new IPEndPoint(host, ProbePort), ct).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }

            foreach (var subnet in extraSubnets ?? [])
            {
                if (!LanProtocol.TryParseSubnet(subnet, out _, out var list)) continue;
                foreach (var host in list)
                {
                    try
                    {
                        await udp.SendAsync(probe, new IPEndPoint(host, ProbePort), ct).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            await CollectAsync(udp, rooms, TimeSpan.FromMilliseconds(TimeoutMs * 0.6), ct).ConfigureAwait(false);
        }

        return rooms.Values
            .Where(room => !IPAddress.IsLoopback(room.Host))
            .OrderBy(room => room.Name, StringComparer.Ordinal)
            .ThenBy(room => room.Address, StringComparer.Ordinal)
            .ToList();
    }

    private static async Task CollectAsync(
        UdpClient udp, Dictionary<string, LanRoom> rooms, TimeSpan window, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(window);

        while (!deadline.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await udp.ReceiveAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                continue;
            }

            var text = Encoding.UTF8.GetString(received.Buffer);
            if (!LanProtocol.TryParseReply(text, received.RemoteEndPoint.Address, out var room)) continue;

            rooms[$"{room.Host}:{room.Port}"] = room;
        }
    }
}
