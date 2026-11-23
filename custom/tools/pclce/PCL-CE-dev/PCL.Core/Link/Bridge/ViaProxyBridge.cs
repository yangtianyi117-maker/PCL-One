using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;

namespace PCL.Core.Link.Bridge;

/// <summary>
/// 加入者本机的 ViaProxy 进程。只监听 loopback，把客户端协议翻译成房主协议。
/// </summary>
public sealed class ViaProxyBridge : IAsyncDisposable
{
    private readonly Process _process;
    private readonly StringBuilder _stderr = new();

    private ViaProxyBridge(Process process, int localPort)
    {
        _process = process;
        LocalPort = localPort;
    }

    public int LocalPort { get; }

    public bool HasExited => _process.HasExited;

    public static async Task<ViaProxyBridge> StartAsync(
        string jarPath,
        string javaExePath,
        string hostVersion,
        int targetPort,
        CancellationToken ct = default)
    {
        if (!File.Exists(jarPath))
            throw new FileNotFoundException("ViaProxy jar 不存在", jarPath);
        if (!File.Exists(javaExePath))
            throw new FileNotFoundException("Java 不存在", javaExePath);

        var localPort = GetFreePort();
        var psi = new ProcessStartInfo
        {
            FileName = javaExePath,
            WorkingDirectory = Path.GetDirectoryName(jarPath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("-jar");
        psi.ArgumentList.Add(jarPath);
        psi.ArgumentList.Add("cli");
        psi.ArgumentList.Add("--bind-address");
        psi.ArgumentList.Add($"127.0.0.1:{localPort}");
        psi.ArgumentList.Add("--target-address");
        psi.ArgumentList.Add($"127.0.0.1:{targetPort}");
        psi.ArgumentList.Add("--target-version");
        psi.ArgumentList.Add(hostVersion);
        psi.ArgumentList.Add("--proxy-online-mode");
        psi.ArgumentList.Add("false");
        psi.ArgumentList.Add("--auth-method");
        psi.ArgumentList.Add("NONE");

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var bridge = new ViaProxyBridge(process, localPort);
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                lock (bridge._stderr) bridge._stderr.AppendLine(e.Data);
        };

        LogWrapper.Info("ViaProxy",
            $"启动翻译代理：client -> 127.0.0.1:{targetPort}（{hostVersion}），本地端口 {localPort}");
        if (!process.Start())
            throw new InvalidOperationException("ViaProxy 进程启动失败");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                string error;
                lock (bridge._stderr) error = bridge._stderr.ToString().Trim();
                throw new InvalidOperationException(
                    $"ViaProxy 提前退出：{(error.Length == 0 ? "无 stderr" : error)}");
            }

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, localPort, ct).ConfigureAwait(false);
                LogWrapper.Info("ViaProxy", $"翻译代理已就绪：127.0.0.1:{localPort}");
                return bridge;
            }
            catch (SocketException)
            {
                await Task.Delay(200, ct).ConfigureAwait(false);
            }
        }

        await bridge.StopAsync().ConfigureAwait(false);
        throw new TimeoutException("ViaProxy 30 秒内没有监听本地端口");
    }

    public async Task StopAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(true);
                await _process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("ViaProxy", $"停止代理失败：{ex.Message}");
        }
        finally
        {
            _process.Dispose();
            LogWrapper.Info("ViaProxy", "翻译代理已停止");
        }
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
