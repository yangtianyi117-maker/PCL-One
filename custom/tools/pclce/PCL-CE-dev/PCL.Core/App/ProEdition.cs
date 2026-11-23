namespace PCL.Core.App;

/// <summary>
/// PCL 定制版 构建标记。
///
/// PCL CE 的自动更新会把运行时替换成上游社区版，覆盖 Pro 的 Link/Phone 能力；
/// Pro 构建必须关闭自动更新，由发布脚本整体替换。保留 CE 的更新代码，
/// 只在 Pro 构建里短路，方便以后接 Pro 自己的更新源。
/// </summary>
public static class ProEdition
{
    public const bool Enabled = true;

    public const string DisplayName = "PCL 定制版";

    public const string VersionSuffix = "pro.26";

    /// <summary>当前 Pro 构建号，用于更新比较；每发一版 +1。</summary>
    public const int BuildNumber = 26;

    /// <summary>当前 Pro 构建 ID，与签名清单的 build 对应。</summary>
    public const string BuildId = "pclpro-2026-11-21.17";
}
