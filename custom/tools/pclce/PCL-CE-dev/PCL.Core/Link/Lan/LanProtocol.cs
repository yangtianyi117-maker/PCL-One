using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace PCL.Core.Link.Lan;

/// <summary>一条要播报的局域网条目：名字 + 游戏端口。</summary>
public readonly record struct LanEntry(string Motd, int Port)
{
    public string Payload => LanProtocol.Announce(Motd, Port);
}

/// <summary>房主应答器回给扫描方的一条房间信息。</summary>
public sealed record LanRoom(string Name, int Port, bool Official, IPAddress Host)
{
    public string Address => $"{Host}:{Port}";
}

/// <summary>
/// 「房主内网是中心」这一层的最小协议集合：
///
///   1) 原版局域网发现：224.0.2.60:4445，"[MOTD]名字[/MOTD][AD]端口[/AD]"
///      —— 26.3 客户端仍在用，所以不用改游戏。
///   2) PCL 定制版 应答协议：UDP 25566，"MCROOM1?" 问、"MCROOM1 端口 是否官方 名字" 答。
///      —— 只在「组播过不去」的网段用（多 VLAN / AP 隔离 / 虚拟网卡），
///         一个包一问一答，不需要任何房间码、目录或公网服务。
///
/// 这里的每个函数都是纯的：不碰网络，方便自检直接断言。
/// </summary>
public static class LanProtocol
{
    public const string GroupAddress = "224.0.2.60";

    /// <summary>原版局域网发现的 UDP 端口。</summary>
    public const int GameAnnouncePort = 4445;

    /// <summary>房主应答器的端口（自定协议，与游戏无关）。</summary>
    public const int ResponderPort = 25566;

    public const string Probe = "MCROOM1?";

    public const string DefaultName = "PCL 定制版";

    private const string ReplyPrefix = "MCROOM1 ";
    private const int MaxNameLength = 64;

    /// <summary>洗干净 MOTD：去颜色码、去会破坏报文的闭合标签，限长。</summary>
    public static string Sanitize(string? motd)
    {
        if (string.IsNullOrWhiteSpace(motd)) return DefaultName;

        var sb = new StringBuilder(motd.Length);
        for (var i = 0; i < motd.Length; i++)
        {
            var c = motd[i];
            if (c == '§')
            {
                i++; // 跳过 § 后面的格式字符
                continue;
            }

            if (c is '\r' or '\n') continue;
            sb.Append(c);
        }

        var text = sb.ToString()
            .Replace("[/MOTD]", "", StringComparison.OrdinalIgnoreCase)
            .Replace("[/AD]", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (text.Length == 0) return DefaultName;
        return text.Length <= MaxNameLength ? text : text[..MaxNameLength];
    }

    /// <summary>原版局域网发现报文。</summary>
    public static string Announce(string motd, int port) =>
        $"[MOTD]{Sanitize(motd)}[/MOTD][AD]{port}[/AD]";

    /// <summary>应答报文。</summary>
    public static string Reply(string name, int port, bool official) =>
        $"{ReplyPrefix}{port} {(official ? 1 : 0)} {Sanitize(name)}";

    public static bool IsProbe(string text) =>
        string.Equals(text.Trim(), Probe, StringComparison.OrdinalIgnoreCase);

    /// <summary>解析应答报文。名字里允许有空格，所以只切前三段。</summary>
    public static bool TryParseReply(string? text, IPAddress host, out LanRoom room)
    {
        room = null!;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var body = text.Trim();
        if (!body.StartsWith(ReplyPrefix, StringComparison.OrdinalIgnoreCase)) return false;

        var parts = body[ReplyPrefix.Length..].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;
        if (!int.TryParse(parts[0], out var port) || port is < 1 or > 65535) return false;

        var official = parts[1] == "1";
        var name = parts.Length >= 3 ? parts[2].Trim() : "";
        room = new LanRoom(name.Length > 0 ? name : $"房间 {host}", port, official, host);
        return true;
    }

    /// <summary>本机默认出口地址（用「连一个公网地址看本地端点」拿到，不发包）。</summary>
    public static IPAddress? DefaultInterface()
    {
        try
        {
            using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            probe.Connect(new IPEndPoint(IPAddress.Parse("223.5.5.5"), 53));
            if (probe.LocalEndPoint is IPEndPoint ep && !IPAddress.IsLoopback(ep.Address))
                return ep.Address;
        }
        catch
        {
            // ignored
        }

        return Interfaces()
            .Select(i => i.Address)
            .FirstOrDefault(a => !IPAddress.IsLoopback(a) && !a.ToString().StartsWith("169.254."));
    }

    /// <summary>本机处于活动状态的 IPv4 网卡（地址 + 前缀长度）。</summary>
    public static List<(IPAddress Address, int Prefix, IPAddress Mask)> Interfaces()
    {
        var list = new List<(IPAddress, int, IPAddress)>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var ua in nic.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(ua.Address)) continue;
                    var mask = ua.IPv4Mask;
                    if (mask is null) continue;
                    list.Add((ua.Address, PrefixOf(mask), mask));
                }
            }
        }
        catch
        {
            // ignored
        }

        return list;
    }

    /// <summary>把子网掩码转成前缀长度（255.255.255.0 → 24）。</summary>
    public static int PrefixOf(IPAddress mask)
    {
        var bits = 0;
        foreach (var b in mask.GetAddressBytes())
        {
            if (b == 0xFF)
            {
                bits += 8;
                continue;
            }

            for (var bit = 7; bit >= 0 && (b & (1 << bit)) != 0; bit--) bits++;
            break;
        }

        return bits;
    }

    /// <summary>网段的定向广播地址（/24 的 192.168.1.0 → 192.168.1.255）。</summary>
    public static IPAddress BroadcastOf(IPAddress address, IPAddress mask)
    {
        var a = address.GetAddressBytes();
        var m = mask.GetAddressBytes();
        var b = new byte[4];
        for (var i = 0; i < 4; i++) b[i] = (byte)(a[i] | (byte)~m[i]);
        return new IPAddress(b);
    }

    /// <summary>要发的广播/探测目标：本机每个网段的定向广播 + 受限广播。</summary>
    public static List<IPAddress> BroadcastTargets()
    {
        var targets = new List<IPAddress> { IPAddress.Broadcast };
        foreach (var (address, _, mask) in Interfaces())
        {
            var b = BroadcastOf(address, mask);
            if (!targets.Contains(b)) targets.Add(b);
        }

        return targets;
    }

    /// <summary>某个网段里除自己以外的所有主机地址（用于「广播被挡」时的兜底扫描）。</summary>
    public static List<IPAddress> ScanTargets(int maxPerSubnet = 512)
    {
        var self = Interfaces().Select(i => i.Address).ToHashSet();
        var result = new List<IPAddress>();

        foreach (var (address, prefix, mask) in Interfaces())
        {
            if (prefix is < 22 or > 30) continue; // 太大或太小的网段不扫

            var m = mask.GetAddressBytes();
            var baseBytes = new byte[4];
            for (var i = 0; i < 4; i++) baseBytes[i] = (byte)(address.GetAddressBytes()[i] & m[i]);
            var baseValue = (uint)(baseBytes[0] << 24 | baseBytes[1] << 16 | baseBytes[2] << 8 | baseBytes[3]);

            var hostBits = 32 - prefix;
            var count = (int)Math.Min(1u << hostBits, (uint)maxPerSubnet);
            for (var i = 1; i < count; i++)
            {
                var value = baseValue + (uint)i;
                var ip = new IPAddress(new[]
                {
                    (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value
                });
                if (self.Contains(ip) || ip.Equals(IPAddress.Broadcast)) continue;
                result.Add(ip);
            }
        }

        return result;
    }

    /// <summary>把 "10.0.8.0/24" 解析成广播地址（跨 VLAN 手动指定网段时用）。</summary>
    public static bool TryParseSubnet(string? text, out IPAddress broadcast, out List<IPAddress> hosts)
    {
        broadcast = IPAddress.None;
        hosts = new List<IPAddress>();
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Split('/', 2);
        if (!IPAddress.TryParse(parts[0], out var ip)) return false;

        var mask = parts.Length > 1
            ? PrefixToMask(int.Parse(parts[1]))
            : IPAddress.Parse("255.255.255.0");

        var m = mask.GetAddressBytes();
        var baseBytes = new byte[4];
        for (var i = 0; i < 4; i++) baseBytes[i] = (byte)(ip.GetAddressBytes()[i] & m[i]);
        var baseValue = (uint)(baseBytes[0] << 24 | baseBytes[1] << 16 | baseBytes[2] << 8 | baseBytes[3]);

        broadcast = BroadcastOf(ip, mask);
        var count = 1 << (32 - PrefixOf(mask));
        for (var i = 1; i < Math.Min(count, 512); i++)
        {
            var v = baseValue + (uint)i;
            hosts.Add(new IPAddress(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }));
        }

        return true;
    }

    private static IPAddress PrefixToMask(int prefix)
    {
        prefix = Math.Clamp(prefix, 0, 32);
        var value = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        return new IPAddress(new[]
        {
            (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value
        });
    }

    /// <summary>某个地址是否和本机同一网段（同网段时原版组播本来就能到，不需要我们再转一手）。</summary>
    public static bool IsLocalSubnet(IPAddress address)
    {
        var target = address.GetAddressBytes();
        foreach (var (local, _, mask) in Interfaces())
        {
            var l = local.GetAddressBytes();
            var m = mask.GetAddressBytes();
            var same = true;
            for (var i = 0; i < 4; i++)
                if ((l[i] & m[i]) != (target[i] & m[i]))
                {
                    same = false;
                    break;
                }

            if (same) return true;
        }

        return false;
    }
}
