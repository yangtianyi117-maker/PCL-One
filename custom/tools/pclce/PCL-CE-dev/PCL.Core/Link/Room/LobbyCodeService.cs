using System;
using System.Linq;
using PCL.Core.Link.Scaffolding;

namespace PCL.Core.Link.Room;

/// <summary>
/// 房间码的唯一入口。
///
/// 第一性原理：一个房间只需要一个身份。PCL CE 的 Scaffolding 协议已经定义了
/// <c>U/XXXX-XXXX-XXXX-XXXX</c>（网络名 + 网络密钥都从码里派生），所以全产品只用这一种码；
/// 旧版 McLauncher 的 <c>ML-</c> 码和旧 PCL CE/Terracotta 码只作为只读兼容，不再生成、不再加入。
/// </summary>
public static class LobbyCodeService
{
    public const string Prefix = "U/";

    /// <summary>生成一个新的房间码。</summary>
    public static LobbyCode Generate()
    {
        var info = LobbyCodeGenerator.Generate();
        return new LobbyCode(info.FullCode, info.NetworkName, info.NetworkSecret, LobbyCodeKind.PclCe);
    }

    /// <summary>
    /// 归一化并解析房间码。允许用户输入小写、无连字符、带空格的码。
    /// </summary>
    public static bool TryParse(string? raw, out LobbyCode code)
    {
        code = LobbyCode.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var text = Normalize(raw);
        if (!LobbyCodeGenerator.TryParse(text, out var info)) return false;

        code = new LobbyCode(info.FullCode, info.NetworkName, info.NetworkSecret, LobbyCodeKind.PclCe);
        return true;
    }

    /// <summary>解析失败时抛出带用户可读信息的异常。</summary>
    public static LobbyCode ParseOrThrow(string? raw)
    {
        if (TryParse(raw, out var code)) return code;
        throw new ArgumentException("房间码无效，应为 U/XXXX-XXXX-XXXX-XXXX", nameof(raw));
    }

    /// <summary>
    /// 只读兼容：识别旧版 PCL CE base32 码、Terracotta 码、McLauncher 的 ML- 码。
    /// 这些码不能用于当前 Scaffolding 入房，只用于展示/迁移时给出明确提示。
    /// </summary>
    public static bool IsLegacyCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var text = raw.Trim().ToUpperInvariant().Replace(" ", "");
        if (text.StartsWith("ML-", StringComparison.Ordinal)) return true;
        if (text.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        if (text.Contains('-')) return true;
        if (text.Length >= 9 && text.All(char.IsLetterOrDigit)) return true;
        return false;
    }

    /// <summary>归一化：去空白、转大写、补 U/ 前缀、补连字符。</summary>
    public static string Normalize(string raw)
    {
        var text = raw.Trim().ToUpperInvariant().Replace(" ", "").Replace("\t", "");

        // 去掉常见的复制噪声：引号、行首尾标点
        text = text.Trim('"', '\'', '`', ',', '，', '。', ';', '；');

        if (!text.StartsWith(Prefix, StringComparison.Ordinal))
        {
            // 允许只给 16 位数据；也允许给 18 位无连字符正文
            if (text.StartsWith("U", StringComparison.Ordinal) && text.Length == 19)
                text = Prefix + text[1..];
            else if (text.Length is 16 or 18)
                text = Prefix + text;
        }

        if (!text.StartsWith(Prefix, StringComparison.Ordinal)) return text;

        var body = text[Prefix.Length..].Replace("-", "");
        if (body.Length != 16) return text;

        // U/ 之后按 4-4-4-4 重新分组；LobbyCodeGenerator 的校验位算法不变
        var grouped = string.Join('-', body[..4], body[4..8], body[8..12], body[12..16]);
        return Prefix + grouped;
    }
}

/// <summary>解析后的房间身份。</summary>
public sealed record LobbyCode(string FullCode, string NetworkName, string NetworkSecret, LobbyCodeKind Kind)
{
    public static readonly LobbyCode Empty = new(string.Empty, string.Empty, string.Empty, LobbyCodeKind.PclCe);

    public bool IsEmpty => string.IsNullOrEmpty(FullCode);

    public override string ToString() => FullCode;
}

public enum LobbyCodeKind
{
    PclCe,
    Terracotta
}
