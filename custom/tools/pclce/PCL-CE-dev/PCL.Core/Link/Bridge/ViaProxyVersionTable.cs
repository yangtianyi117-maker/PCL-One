using System;
using System.Collections.Generic;

namespace PCL.Core.Link.Bridge;

/// <summary>
/// PCL 定制版 跨版本互通的白名单。只允许这些热门版本互相翻译；
/// 其它版本仍走房主精确版本安装，避免未验证的协议组合。
/// </summary>
public static class ViaProxyVersionTable
{
    /// <summary>
    /// 10 个热门互通版本（1.20.1 - 26.3）。
    /// </summary>
    public static IReadOnlyList<string> SupportedVersions { get; } =
    [
        "1.20.1",
        "1.20.4",
        "1.20.6",
        "1.21",
        "1.21.1",
        "1.21.3",
        "1.21.4",
        "1.21.5",
        "1.21.8",
        "26.3"
    ];

    private static readonly HashSet<string> Supported =
        new(SupportedVersions, StringComparer.OrdinalIgnoreCase);

    public static bool IsSupported(string? version) =>
        !string.IsNullOrWhiteSpace(version) && Supported.Contains(version.Trim());
}
