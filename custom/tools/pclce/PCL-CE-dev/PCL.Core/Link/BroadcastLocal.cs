using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;

namespace PCL.Core.Link;

/// <summary>
/// 把 PCL 房间广播成 Minecraft 原生 LAN 世界，使客户端在
/// “Play Multiplayer → Scanning for games on your local network” 中直接看到。
/// </summary>
public class BroadcastLocal(string description, int localPort) : IDisposable
{
    private const int MinecraftLanPort = 4445;
    private static readonly IPAddress MulticastV4 = IPAddress.Parse("224.0.2.60");
    private static readonly IPAddress MulticastV6 = IPAddress.Parse("ff75:230::60");

    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public void Start()
    {
        if (_isRunning) return;

        _cts = new CancellationTokenSource();
        _isRunning = true;
        _ = Task.Run(() => _RunUdpBroadcastAsync(_cts.Token), _cts.Token);

        LogWrapper.Info("LanBroadcast",
            $"开始向 Minecraft LAN 发现广播：{description}，端口 {localPort}");
    }

    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;
        try { _cts?.Cancel(); } catch (Exception) { /* best effort */ }
        _cts?.Dispose();
        _cts = null;

        LogWrapper.Info("LanBroadcast", "停止 Minecraft LAN 发现广播");
    }

    private async Task _RunUdpBroadcastAsync(CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(
            $"[MOTD]{description}[/MOTD][AD]{localPort}[/AD]");

        try
        {
            using var ipv4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            {
                EnableBroadcast = true
            };
            ipv4.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
            ipv4.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);

            Socket? ipv6 = null;
            try
            {
                ipv6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                ipv6.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastTimeToLive, 1);
                ipv6.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastLoopback, true);
            }
            catch (Exception ex)
            {
                LogWrapper.Debug("LanBroadcast", $"IPv6 广播不可用：{ex.Message}");
            }

            using (ipv6)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Minecraft 的 LAN 发现监听 224.0.2.60:4445（IPv4）和
                    // ff75:230::60:4445（IPv6），必须按组播发送，不能只发 127.0.0.1。
                    TrySend(ipv4, new IPEndPoint(MulticastV4, MinecraftLanPort), payload);
                    TrySend(ipv4, new IPEndPoint(IPAddress.Broadcast, MinecraftLanPort), payload);
                    TrySend(ipv4, new IPEndPoint(IPAddress.Loopback, MinecraftLanPort), payload);

                    if (ipv6 is not null)
                        TrySend(ipv6, new IPEndPoint(MulticastV6, MinecraftLanPort), payload);

                    try
                    {
                        await Task.Delay(1500, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("LanBroadcast", $"LAN 广播任务结束：{ex.Message}");
        }
    }

    private static void TrySend(Socket socket, IPEndPoint endpoint, byte[] payload)
    {
        try
        {
            socket.SendTo(payload, endpoint);
        }
        catch (Exception ex)
        {
            // 某个网卡/协议不可用时继续尝试其它目标。
            LogWrapper.Debug("LanBroadcast", $"发送 {endpoint} 失败：{ex.Message}");
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
