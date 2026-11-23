using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link.Lan;

/// <summary>
/// 房主侧的「零输入开关」：盯着游戏的 latest.log，玩家一点「对局域网开放」，
/// 我们立刻把应答器拉起来；玩家关掉，就收回去。
///
/// 第一性原理：房主不需要在启动器里点任何东西 —— 端口是游戏自己选的，
/// 日志里已经写了（26.3：<c>Published LAN server on port {}</c>；1.12.2：<c>Started serving on {}</c>），
/// 我们只要把这句话读出来。
/// </summary>
public sealed partial class LanWorldWatcher : IDisposable
{
    [GeneratedRegex(@"Published LAN server on port (\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex Published26();

    [GeneratedRegex(@"Started serving on (\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PublishedLegacy();

    [GeneratedRegex(@"Unpublishing integrated server", RegexOptions.IgnoreCase)]
    private static partial Regex Unpublished();

    private readonly string _logPath;
    private readonly string _name;
    private readonly bool _official;
    private readonly int _listenPort;
    private readonly Action<string>? _log;
    private readonly object _gate = new();
    private LanResponder? _responder;
    private long _offset;

    public LanWorldWatcher(
        string mcDir,
        string? name = null,
        bool official = false,
        int listenPort = LanProtocol.ResponderPort,
        Action<string>? log = null)
    {
        _logPath = Path.Combine(mcDir, "logs", "latest.log");
        _name = LanProtocol.Sanitize(name);
        _official = official;
        _listenPort = listenPort;
        _log = log;
    }

    public LanResponder? Current
    {
        get { lock (_gate) return _responder; }
    }

    /// <summary>明确端口：不盯日志，直接开应答器（调试/脚本用）。</summary>
    public async Task RunFixedAsync(int gamePort, CancellationToken ct)
    {
        Start(gamePort);
        try
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // expected
        }
        finally
        {
            Stop();
        }
    }

    /// <summary>盯日志模式。</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        ResetOffset();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                foreach (var line in ReadNewLines())
                {
                    if (Published26().Match(line) is { Success: true } m26)
                        Start(int.Parse(m26.Groups[1].Value));
                    else if (PublishedLegacy().Match(line) is { Success: true } mOld)
                        Start(int.Parse(mOld.Groups[1].Value));
                    else if (Unpublished().IsMatch(line))
                        Stop();
                }
            }
            catch (IOException)
            {
                // 日志正被游戏写，下一轮再读
            }
            catch (UnauthorizedAccessException)
            {
                // ignored
            }

            try
            {
                await Task.Delay(1000, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        Stop();
    }

    private void ResetOffset()
    {
        try
        {
            _offset = File.Exists(_logPath) ? new FileInfo(_logPath).Length : 0;
        }
        catch
        {
            _offset = 0;
        }
    }

    private IEnumerable<string> ReadNewLines()
    {
        if (!File.Exists(_logPath)) yield break;

        using var fs = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (fs.Length < _offset) _offset = 0; // 日志被轮转/清空
        if (fs.Length == _offset) yield break;

        fs.Seek(_offset, SeekOrigin.Begin);
        var buffer = new byte[fs.Length - _offset];
        var read = fs.Read(buffer, 0, buffer.Length);
        _offset += read;

        var text = Encoding.UTF8.GetString(buffer, 0, read);
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.Length > 0) yield return trimmed;
        }
    }

    private void Start(int gamePort)
    {
        if (gamePort is < 1 or > 65535) return;
        lock (_gate)
        {
            if (_responder is not null && _responder.GamePort == gamePort) return;
            _responder?.Dispose();
            _responder = null;
            try
            {
                _responder = new LanResponder(gamePort, _name, _official, _listenPort);
                _log?.Invoke($"[房主] 房间已在 UDP {_listenPort} 应答：端口 {gamePort}，名字「{_name}」");
            }
            catch (Exception ex)
            {
                _log?.Invoke("[房主] 应答器没起来：" + ex.Message);
            }
        }
    }

    private void Stop()
    {
        lock (_gate)
        {
            if (_responder is null) return;
            _responder.Dispose();
            _responder = null;
            _log?.Invoke("[房主] 已收回房间应答");
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
