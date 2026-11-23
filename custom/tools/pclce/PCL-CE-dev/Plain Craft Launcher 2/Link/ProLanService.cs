using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PCL.Core.App;
using PCL.Core.Link.Lan;

namespace PCL;

/// <summary>
/// 房主侧局域网应答服务：只要本机有 Minecraft 在跑，就盯着该实例的 latest.log；
/// 玩家点「对局域网开放」后自动开应答器，关闭时自动收回。
///
/// 它不依赖联机页是否打开，也不依赖是否创建了大厅：
/// 同一网段的玩家用原版组播就能看到房间；跨 VLAN/AP 隔离的玩家靠这个 UDP 应答找到端口。
/// </summary>
public static class ProLanService
{
    private sealed class RunningHost
    {
        public required LanWorldWatcher Watcher { get; init; }
        public required CancellationTokenSource Cts { get; init; }
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<int, RunningHost> Hosts = new();
    private static Timer? _timer;

    public static void Start()
    {
        lock (Gate)
        {
            if (_timer is not null) return;
            _timer = new Timer(_ => Scan(), null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
            Common.Log("[LAN] 局域网应答服务已启动");
        }
    }

    public static void Stop()
    {
        lock (Gate)
        {
            _timer?.Dispose();
            _timer = null;
            foreach (var host in Hosts.Values)
            {
                try { host.Cts.Cancel(); } catch { }
                host.Watcher.Dispose();
                host.Cts.Dispose();
            }

            Hosts.Clear();
        }
    }

    private static void Scan()
    {
        try
        {
            if (!Config.Link.LanEnabled) return;

            var alive = new HashSet<int>();
            foreach (var watcher in GameWatcher.mcWatcherList.ToArray())
            {
                if (watcher.State is GameWatcher.Watcher.MinecraftState.Ended
                    or GameWatcher.Watcher.MinecraftState.Crashed
                    or GameWatcher.Watcher.MinecraftState.Canceled)
                    continue;

                var process = watcher.gameProcess;
                if (process is null || process.HasExited) continue;

                var instance = watcher.version;
                var path = instance?.PathIndie;
                if (string.IsNullOrWhiteSpace(path)) continue;

                alive.Add(process.Id);
                lock (Gate)
                {
                    if (Hosts.ContainsKey(process.Id)) continue;

                    var cts = new CancellationTokenSource();
                    var host = new LanWorldWatcher(
                        path!,
                        string.IsNullOrWhiteSpace(instance!.Name) ? "PCL 定制版" : instance.Name,
                        false,
                        Config.Link.LanPort,
                        message => Common.Log(message));
                    Hosts[process.Id] = new RunningHost { Watcher = host, Cts = cts };
                    _ = host.RunAsync(cts.Token);
                    Common.Log($"[LAN] 开始监听实例「{instance.Name}」的局域网世界");
                }
            }

            lock (Gate)
            {
                foreach (var (pid, host) in Hosts.ToArray())
                {
                    if (alive.Contains(pid)) continue;
                    try { host.Cts.Cancel(); } catch { }
                    host.Watcher.Dispose();
                    host.Cts.Dispose();
                    Hosts.Remove(pid);
                    Common.Log($"[LAN] 游戏 {pid} 已退出，收回局域网应答");
                }
            }
        }
        catch (Exception ex)
        {
            Common.Log(ex, "[LAN] 局域网应答扫描失败");
        }
    }
}
