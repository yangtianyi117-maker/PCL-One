using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.Link.Bedrock;
using PCL.Core.Logging;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;

namespace PCL;

public enum BedrockBridgeState
{
    Stopped,
    Downloading,
    WaitingForWorld,
    Starting,
    Running,
    Error
}

public sealed record BedrockBridgeSnapshot(
    BedrockBridgeState State,
    string Message,
    string Address,
    int WorldPort);

/// <summary>
/// Hosts the local "same Wi-Fi" Bedrock bridge:
/// the player opens a world to LAN, we watch latest.log, and start Geyser against that port.
///
/// It is intentionally opt-in (a button), because it opens 0.0.0.0:19132 on the machine.
/// </summary>
public static class ProBedrockBridgeService
{
    private static readonly object Gate = new();
    private static readonly Regex Published26 =
        new(@"Published LAN server on port (\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PublishedLegacy =
        new(@"Started serving on (\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Unpublished =
        new(@"Unpublishing integrated server", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static CancellationTokenSource? _cts;
    private static Task? _loop;
    private static Process? _geyser;
    private static BedrockBridgeState _state = BedrockBridgeState.Stopped;
    private static string _message = "未启动";
    private static string _address = "";
    private static int _worldPort;

    public static event Action? Changed;

    public static BedrockBridgeSnapshot Current
    {
        get
        {
            lock (Gate)
            {
                return new BedrockBridgeSnapshot(_state, _message, _address, _worldPort);
            }
        }
    }

    public static async Task StartAsync(int bedrockPort = 19132)
    {
        lock (Gate)
        {
            if (_state is not BedrockBridgeState.Stopped and not BedrockBridgeState.Error)
                return;
            _state = BedrockBridgeState.Downloading;
            _message = "准备 Geyser…";
            _address = "";
            _worldPort = 0;
        }

        Notify();

        var cts = new CancellationTokenSource();
        lock (Gate)
        {
            _cts = cts;
        }

        _loop = Task.Run(() => RunAsync(bedrockPort, cts.Token), cts.Token);
        await Task.CompletedTask;
    }

    public static void Stop()
    {
        CancellationTokenSource? cts;
        lock (Gate)
        {
            cts = _cts;
            _cts = null;
        }

        try
        {
            cts?.Cancel();
        }
        catch
        {
            // best effort
        }

        StopGeyser();
        lock (Gate)
        {
            _state = BedrockBridgeState.Stopped;
            _message = "未启动";
            _address = "";
            _worldPort = 0;
        }

        Notify();
        cts?.Dispose();
    }

    private static async Task RunAsync(int bedrockPort, CancellationToken ct)
    {
        try
        {
            var progress = new Progress<string>(text => SetState(BedrockBridgeState.Downloading, text));
            var jar = await GeyserProvisioner.EnsureAsync(progress, ct).ConfigureAwait(false);

            // 自动启动时 Java 管理器可能还没扫描完；最多等 10 秒。
            string? java = null;
            for (var i = 0; i < 10 && java is null && !ct.IsCancellationRequested; i++)
            {
                java = FindJava();
                if (java is null) await Task.Delay(1000, ct).ConfigureAwait(false);
            }

            if (java is null)
                throw new InvalidOperationException("没有找到 Java 21+，无法启动 Geyser");

            // 自动启动时，实例列表可能还没加载完；最多等 15 秒。
            var instance = InstanceList.McMcInstanceSelected;
            for (var i = 0; i < 15 && instance is null && !ct.IsCancellationRequested; i++)
            {
                SetState(BedrockBridgeState.WaitingForWorld, "等待游戏实例加载…");
                await Task.Delay(1000, ct).ConfigureAwait(false);
                instance = InstanceList.McMcInstanceSelected;
            }

            if (instance is null)
                throw new InvalidOperationException("请先选择一个游戏实例");
            var logPath = Path.Combine(instance.PathIndie, "logs", "latest.log");

            var watcher = new LocalWorldPortWatcher(logPath);
            var lastPort = watcher.Poll();
            if (lastPort > 0)
            {
                var started = await StartGeyserAsync(java, jar, lastPort, bedrockPort, ct).ConfigureAwait(false);
                if (!started) return;
            }
            else
            {
                SetState(BedrockBridgeState.WaitingForWorld, "等待游戏内「对局域网开放」");
            }

            while (!ct.IsCancellationRequested)
            {
                var port = watcher.Poll();
                if (port > 0)
                {
                    if (_geyser is null || _worldPort != port)
                    {
                        StopGeyser();
                        var started = await StartGeyserAsync(java, jar, port, bedrockPort, ct).ConfigureAwait(false);
                        if (!started) return;
                    }
                }
                else if (_geyser is not null)
                {
                    StopGeyser();
                    SetState(BedrockBridgeState.WaitingForWorld, "世界已关闭，等待再次对局域网开放");
                }

                await Task.Delay(1000, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // expected
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Bedrock", "基岩桥运行失败");
            SetState(BedrockBridgeState.Error, "基岩桥失败：" + ex.Message);
            StopGeyser();
        }
        finally
        {
            if (ct.IsCancellationRequested)
            {
                StopGeyser();
            }
        }
    }

    private static async Task<bool> StartGeyserAsync(string java, string jar, int worldPort, int bedrockPort,
        CancellationToken ct)
    {
        GeyserProvisioner.WriteConfig(worldPort, bedrockPort);
        SetState(BedrockBridgeState.Starting, $"Geyser 启动中（127.0.0.1:{worldPort} → 0.0.0.0:{bedrockPort}）");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = java,
                WorkingDirectory = GeyserProvisioner.GeyserDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };
        process.StartInfo.ArgumentList.Add("-Xms128M");
        process.StartInfo.ArgumentList.Add("-Xmx512M");
        process.StartInfo.ArgumentList.Add("-jar");
        process.StartInfo.ArgumentList.Add(jar);
        process.StartInfo.ArgumentList.Add("--nogui");

        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        process.OutputDataReceived += (_, e) => HandleGeyserLine(e.Data, ready);
        process.ErrorDataReceived += (_, e) => HandleGeyserLine(e.Data, ready);
        process.Exited += (_, _) => ready.TrySetResult(false);

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            process.Dispose();
            throw new InvalidOperationException("启动 Geyser 失败：" + ex.Message, ex);
        }

        lock (Gate)
        {
            _geyser = process;
            _worldPort = worldPort;
            _address = $"{GetLanIp()}:{bedrockPort}";
        }

        var completed = await Task.WhenAny(ready.Task, Task.Delay(TimeSpan.FromSeconds(35), ct))
            .ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        var isReady = completed == ready.Task && ready.Task.Result;
        if (!isReady && IsUdpPortListening(bedrockPort))
        {
            // 不同 Geyser 版本的“就绪”日志语言不一致；端口已经在监听就视为成功。
            isReady = true;
        }

        if (!isReady)
        {
            StopGeyser();
            throw new InvalidOperationException("Geyser 没有在 35 秒内就绪，请查看 PCL 日志");
        }

        SetState(BedrockBridgeState.Running,
            $"已启动：手机进入 {(string.IsNullOrWhiteSpace(_address) ? "本机 IP" : _address)}");
        return true;
    }

    private static void HandleGeyserLine(string? line, TaskCompletionSource<bool> ready)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        LogWrapper.Info("Geyser", line);
        if (line.Contains("Started Geyser", StringComparison.OrdinalIgnoreCase) ||
            (line.Contains("Geyser", StringComparison.OrdinalIgnoreCase) &&
             (line.Contains("启动", StringComparison.Ordinal) || line.Contains("UDP", StringComparison.OrdinalIgnoreCase))))
            ready.TrySetResult(true);
    }

    private static bool IsUdpPortListening(int port)
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveUdpListeners()
                .Any(endpoint => endpoint.Port == port);
        }
        catch
        {
            return false;
        }
    }

    private static void StopGeyser()
    {
        Process? process;
        lock (Gate)
        {
            process = _geyser;
            _geyser = null;
        }

        if (process is null) return;
        try
        {
            if (!process.HasExited) process.Kill(true);
            process.WaitForExit(2000);
        }
        catch
        {
            // best effort
        }
        finally
        {
            process.Dispose();
        }
    }

    private static void SetState(BedrockBridgeState state, string message)
    {
        lock (Gate)
        {
            _state = state;
            _message = message;
        }

        Notify();
    }

    private static void Notify()
    {
        try
        {
            Changed?.Invoke();
        }
        catch
        {
            // subscribers must not break the service
        }
    }

    private static string? FindJava()
    {
        try
        {
            var configured = Config.Link.BedrockBridgeJavaPath?.Trim();
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
                return configured;

            var list = JavaService.JavaManager?.GetSortedJavaList();
            if (list is null) return null;
            var entry = list.FirstOrDefault(e =>
                            e.IsEnabled && e.Installation.MajorVersion >= 21 &&
                            e.Installation.Is64Bit && e.Installation.IsStillAvailable)
                        ?? list.FirstOrDefault(e =>
                            e.Installation.MajorVersion >= 21 && e.Installation.IsStillAvailable);
            return entry?.Installation.JavaExePath;
        }
        catch
        {
            return null;
        }
    }

    private static string GetLanIp()
    {
        try
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Select(ua => ua.Address)
                .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                             !IPAddress.IsLoopback(ip))
                .ToList();

            foreach (var ip in candidates)
            {
                var b = ip.GetAddressBytes();
                if (b[0] == 192 && b[1] == 168) return ip.ToString();
                if (b[0] == 10) return ip.ToString();
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return ip.ToString();
            }

            return candidates.FirstOrDefault()?.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    /// <summary>Tracks the last published LAN port in the current instance's latest.log.</summary>
    private sealed class LocalWorldPortWatcher(string logPath)
    {
        private long _offset;
        private int _port;

        public int Poll()
        {
            try
            {
                if (!File.Exists(logPath)) return _port;
                using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (_offset == 0)
                    _offset = Math.Max(0, stream.Length - 2 * 1024 * 1024);
                if (stream.Length < _offset) _offset = 0;
                if (stream.Length == _offset) return _port;

                stream.Seek(_offset, SeekOrigin.Begin);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                while (reader.ReadLine() is { } line)
                {
                    if (Published26.Match(line) is { Success: true } match26)
                        _port = int.Parse(match26.Groups[1].Value);
                    else if (PublishedLegacy.Match(line) is { Success: true } matchOld)
                        _port = int.Parse(matchOld.Groups[1].Value);
                    else if (Unpublished.IsMatch(line))
                        _port = 0;
                }

                _offset = stream.Position;
            }
            catch (IOException)
            {
                // the game is writing; retry next tick
            }
            catch (UnauthorizedAccessException)
            {
                // ignored
            }

            return _port;
        }
    }
}
