using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.Logging;

namespace PCL.Core.Link.Bedrock;

/// <summary>
/// Downloads and configures Geyser Standalone for the local Bedrock bridge.
///
/// Geyser is an independent Java process (MIT-licensed); the launcher only starts it and writes
/// a config that points at the already-published LAN world. Nothing here modifies Minecraft.
/// </summary>
public static class GeyserProvisioner
{
    public const string DownloadUrl =
        "https://download.geysermc.org/v2/projects/geyser/versions/latest/builds/latest/downloads/standalone";

    public static string GeyserDirectory => Path.Combine(Paths.Data, "Geyser");
    public static string JarPath => Path.Combine(GeyserDirectory, "Geyser-Standalone.jar");
    public static string ConfigPath => Path.Combine(GeyserDirectory, "config.yml");

    /// <summary>Ensures the standalone jar exists; downloads it when missing.</summary>
    public static async Task<string> EnsureAsync(IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(GeyserDirectory);

        if (File.Exists(JarPath) && new FileInfo(JarPath).Length > 1_000_000)
            return JarPath;

        progress?.Report("正在下载 Geyser…");
        LogWrapper.Info("Bedrock", "Geyser standalone 不存在，开始下载");

        var temp = JarPath + ".download";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("PCLCustom/1.0 (bedrock bridge)");
            using var response = await http
                .GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using (var output = File.Create(temp))
            {
                await response.Content.CopyToAsync(output, ct).ConfigureAwait(false);
            }

            await using (var check = File.OpenRead(temp))
            {
                var header = new byte[2];
                var read = await check.ReadAsync(header.AsMemory(0, 2), ct).ConfigureAwait(false);
                if (read != 2 || header[0] != 0x50 || header[1] != 0x4B)
                    throw new InvalidDataException("Geyser 下载结果不是有效的 jar 文件");
            }

            File.Move(temp, JarPath, true);
            LogWrapper.Info("Bedrock", $"Geyser 已下载：{JarPath} ({new FileInfo(JarPath).Length} bytes)");
            progress?.Report("Geyser 已就绪");
            return JarPath;
        }
        catch
        {
            try
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            catch
            {
                // best effort
            }

            throw;
        }
    }

    /// <summary>Writes a minimal standalone config for the local world.</summary>
    public static void WriteConfig(int worldPort, int bedrockPort)
    {
        Directory.CreateDirectory(GeyserDirectory);
        var yaml =
            "bedrock:\n" +
            "  address: 0.0.0.0\n" +
            $"  port: {bedrockPort}\n" +
            // 同时接受经典 RakNet 与 1.26.51+ 的 NetherNet；NetherNet 的 WebRTC 走 +1 端口。
            "  transport: both\n" +
            $"  webrtc-port: {Math.Clamp(bedrockPort + 1, 1, 65535)}\n" +
            "  motd1: \"PCL Pro\"\n" +
            "  motd2: \"手机进服\"\n" +
            "remote:\n" +
            "  address: 127.0.0.1\n" +
            $"  port: {worldPort}\n" +
            "  auth-type: offline\n" +
            "  allow-password-authentication: false\n" +
            "passthrough-motd: true\n" +
            "passthrough-player-counts: true\n" +
            "max-players: 12\n" +
            "command-suggestions: true\n" +
            "log-player-ip-addresses: false\n";
        File.WriteAllText(ConfigPath, yaml);
        LogWrapper.Info("Bedrock", $"Geyser 配置已写入：remote=127.0.0.1:{worldPort}, bedrock=0.0.0.0:{bedrockPort}");
    }
}
