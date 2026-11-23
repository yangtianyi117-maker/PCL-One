using System;

namespace PCL.Core.Link.Pack;

/// <summary>
/// 零整合包联机的功能开关。PCL 定制版 构建可打开；普通构建默认关闭，避免在
/// 客户端自动安装管线尚未接入时向旧客户端发布无法消费的清单。
/// </summary>
public static class PackFeature
{
    public static bool Enabled { get; set; } = _ReadDefault();

    private static bool _ReadDefault()
    {
        // PCL 定制版 默认开启；可用 PCL_PRO_PACK_SYNC=0 临时关闭。
        return !string.Equals(
            Environment.GetEnvironmentVariable("PCL_PRO_PACK_SYNC"),
            "0",
            StringComparison.OrdinalIgnoreCase);
    }
}
