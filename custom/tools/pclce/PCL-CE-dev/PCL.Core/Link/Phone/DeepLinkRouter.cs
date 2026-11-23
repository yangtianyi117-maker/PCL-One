using System;
using System.Linq;
using Microsoft.Win32;
using PCL.Core.Link.Room;

namespace PCL.Core.Link.Phone;

/// <summary>一条「点链接直接加入」的解析结果。</summary>
public sealed record JoinLink(string Code, string Raw);

/// <summary>
/// pclpro:// 深链：把「房间码」编码进一个 URL，注册成系统协议处理器。
///
/// 第一性原理：二维码/链接能带的信息只有字符串，而「加入房间」需要的恰好就是字符串。
/// 所以只要让系统把 URL 交给我们，加入就可以零输入。注册表只是一个
/// 「URL → 命令行参数」的转发规则，不引入任何常驻服务。
/// </summary>
public static class DeepLinkRouter
{
    public const string Scheme = "pclpro";
    private const string ClassKey = @"Software\Classes\" + Scheme;

    public static bool TryParse(string? text, out JoinLink link)
    {
        link = new JoinLink("", "");
        if (string.IsNullOrWhiteSpace(text)) return false;

        var raw = text.Trim().Trim('"');

        // 直接粘贴房间码也当作深链处理，方便命令行/脚本
        if (!raw.StartsWith(Scheme + ":", StringComparison.OrdinalIgnoreCase))
        {
            if (!LobbyCodeService.TryParse(raw, out var bare)) return false;
            link = new JoinLink(bare.FullCode, raw);
            return true;
        }

        // pclpro://join?c=U/...
        var queryStart = raw.IndexOf('?');
        string? codeText = null;
        if (queryStart >= 0)
        {
            foreach (var pair in raw[(queryStart + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var index = pair.IndexOf('=');
                if (index <= 0) continue;
                if (string.Equals(pair[..index], "c", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pair[..index], "code", StringComparison.OrdinalIgnoreCase))
                    codeText = Uri.UnescapeDataString(pair[(index + 1)..]);
            }
        }

        if (string.IsNullOrWhiteSpace(codeText) ||
            !LobbyCodeService.TryParse(codeText, out var code))
            return false;

        link = new JoinLink(code.FullCode, raw);
        return true;
    }

    /// <summary>从命令行参数里找出第一条深链。</summary>
    public static JoinLink? FromArgs(System.Collections.Generic.IEnumerable<string> args)
    {
        foreach (var arg in args)
            if (TryParse(arg, out var link))
                return link;
        return null;
    }

    public static string ExePath =>
        Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";

    /// <summary>注册到当前用户（HKCU，不需要管理员）。</summary>
    public static bool Register(out string message)
    {
        if (!OperatingSystem.IsWindows())
        {
            message = "深链注册仅支持 Windows";
            return false;
        }

        try
        {
            var exe = ExePath;
            if (string.IsNullOrWhiteSpace(exe))
            {
                message = "无法确定当前 exe 路径";
                return false;
            }

            using var key = Registry.CurrentUser.CreateSubKey(ClassKey, writable: true);
            if (key is null)
            {
                message = "打不开注册表键";
                return false;
            }

            key.SetValue("", "URL:PCL 定制版 Protocol");
            key.SetValue("URL Protocol", "");
            using var command = key.CreateSubKey(@"shell\open\command", writable: true);
            command?.SetValue("", $"\"{exe}\" join-code --url \"%1\"");
            message = $"已注册 {Scheme}:// → {exe}";
            return true;
        }
        catch (Exception ex)
        {
            message = "注册失败：" + ex.Message;
            return false;
        }
    }

    public static bool IsRegistered(out string? command)
    {
        command = null;
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ClassKey + @"\shell\open\command");
            command = key?.GetValue("") as string;
            return !string.IsNullOrWhiteSpace(command);
        }
        catch
        {
            return false;
        }
    }

    public static bool Unregister()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(ClassKey, throwOnMissingSubKey: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>从 pclpro://join?c=... 生成可分享的链接。</summary>
    public static string BuildJoinUrl(LobbyCode code) =>
        $"{Scheme}://join?c={Uri.EscapeDataString(code.FullCode)}";
}
