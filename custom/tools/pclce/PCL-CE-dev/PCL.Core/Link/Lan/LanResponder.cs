using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link.Lan;

/// <summary>
/// 房主侧应答器：让「不在同一个广播域」的玩家也能找到这个房间。
///
/// 为什么需要它：原版靠 224.0.2.60 组播发现，组播不跨网段（VLAN、AP 隔离、
/// 某些虚拟网卡直接吞掉），于是同网段能看见、隔壁网段看不见。这里补一个
/// 一问一答的 UDP 应答器 —— 客户端往本机网段的广播（或逐个地址）发
/// "MCROOM1?"，房主回 "MCROOM1 端口 是否官方 名字"。
///
/// 它不承担任何「中心」职责：没有房间码、没有注册、没有目录、没有公网服务，
/// 只把「我在这个地址、端口是几」说一遍。房主关掉它，同网段发现照样工作。
/// </summary>
public sealed class LanResponder : IDisposable
{
    private readonly UdpClient _udp;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _gate = new();
    private readonly int _listenPort;
    private int _gamePort;
    private string _name;
    private bool _official;
    private readonly Task _loop;

    public IPEndPoint Endpoint { get; }

    public string Name
    {
        get { lock (_gate) return _name; }
        set { lock (_gate) _name = LanProtocol.Sanitize(value); }
    }

    public int GamePort
    {
        get { lock (_gate) return _gamePort; }
        set
        {
            if (value is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(value));
            lock (_gate) _gamePort = value;
        }
    }

    public bool Official
    {
        get { lock (_gate) return _official; }
        set { lock (_gate) _official = value; }
    }

    public LanResponder(
        int gamePort,
        string? name = null,
        bool official = false,
        int listenPort = LanProtocol.ResponderPort)
    {
        if (gamePort is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(gamePort));

        _gamePort = gamePort;
        _name = LanProtocol.Sanitize(name);
        _official = official;
        _listenPort = listenPort;

        _udp = new UdpClient(AddressFamily.InterNetwork);
        try
        {
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udp.Client.Bind(new IPEndPoint(IPAddress.Any, listenPort));
        }
        catch (SocketException ex)
        {
            _udp.Dispose();
            throw new InvalidOperationException($"应答器端口 {listenPort} 被占用：{ex.Message}", ex);
        }

        Endpoint = (IPEndPoint)_udp.Client.LocalEndPoint!;
        _loop = Task.Run(LoopAsync);
    }

    private async Task LoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await _udp.ReceiveAsync(_cts.Token).ConfigureAwait(false);
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

            try
            {
                var text = Encoding.UTF8.GetString(received.Buffer);
                // 只认探测包，避免被当成放大器
                if (!LanProtocol.IsProbe(text)) continue;

                string reply;
                lock (_gate) reply = LanProtocol.Reply(_name, _gamePort, _official);

                var bytes = Encoding.UTF8.GetBytes(reply);
                await _udp.SendAsync(bytes, received.RemoteEndPoint, _cts.Token).ConfigureAwait(false);
            }
            catch
            {
                // 单个包出错不影响后续
            }
        }
    }

    public string Reply => LanProtocol.Reply(Name, GamePort, Official);

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _udp.Dispose(); } catch { }
        try { _loop.Wait(800); } catch { }
        _cts.Dispose();
    }
}
