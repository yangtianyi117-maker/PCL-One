using System;

namespace PCL.Core.Link.McPing;

/// <summary>
/// 10 个热门版本的协议号。LAN 发现时用当前实例版本握手，避免用旧协议探测新版世界。
/// </summary>
public static class McProtocolVersionTable
{
    public static int Resolve(string? minecraftVersion)
    {
        if (string.IsNullOrWhiteSpace(minecraftVersion)) return 772;
        var version = minecraftVersion.Trim();
        return version switch
        {
            "1.20.1" => 763,
            "1.20.4" => 765,
            "1.20.6" => 766,
            "1.21" or "1.21.1" => 767,
            "1.21.3" => 768,
            "1.21.4" => 769,
            "1.21.5" => 770,
            "1.21.8" => 772,
            "26.3" => 777,
            _ => 772
        };
    }
}
