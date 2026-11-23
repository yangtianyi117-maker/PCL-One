using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;
using PCL.Link.Protocol;

namespace PCL.Core.Link.Room;

/// <summary>
/// 公开房心跳：房主创建公开房后每 30 秒刷新索引记录，离开时删除。
/// </summary>
public static class RoomHeartbeatService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static readonly object _Lock = new();

    private static CancellationTokenSource? _cts;
    private static Task? _task;
    private static RoomRecord? _record;
    private static ECDsa? _key;
    private static IRoomDirectory? _client;

    public static bool IsRunning
    {
        get
        {
            lock (_Lock)
                return _task is { IsCompleted: false };
        }
    }

    public static async Task StartAsync(RoomRecord record, ECDsa key)
    {
        var client = RoomDirectoryFactory.TryCreate();
        if (client is null)
        {
            LogWrapper.Warn("RoomHeartbeat", "公开房目录不可用，跳过发布");
            return;
        }

        await StopAsync().ConfigureAwait(false);

        CancellationTokenSource cts;
        Task task;
        lock (_Lock)
        {
            _record = record;
            _key = key;
            _client = client;
            _cts = cts = new CancellationTokenSource();
            task = _task = Task.Run(() => _LoopAsync(record, key, client, cts.Token));
        }

        // 心跳是后台任务，不阻塞创建大厅。
        _ = task;
    }

    public static async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? task;
        RoomRecord? record;
        ECDsa? key;
        IRoomDirectory? client;

        lock (_Lock)
        {
            cts = _cts;
            task = _task;
            record = _record;
            key = _key;
            client = _client;
            _cts = null;
            _task = null;
            _record = null;
            _key = null;
            _client = null;
        }

        if (cts is null) return;

        try { cts.Cancel(); } catch (Exception) { /* best effort */ }

        if (task is not null)
        {
            try { await task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); }
            catch (Exception) { /* best effort */ }
        }

        if (record is not null && key is not null && client is not null)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await client.DeleteAsync(record.RoomId, key, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogWrapper.Warn("RoomHeartbeat", $"删除公开房记录失败：{ex.Message}");
            }
        }

        client?.Dispose();
        cts.Dispose();
    }

    private static async Task _LoopAsync(
        RoomRecord record, ECDsa key, IRoomDirectory client, CancellationToken ct)
    {
        var failures = 0;
        try
        {
            await client.PublishAsync(record, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            failures++;
            LogWrapper.Warn("RoomHeartbeat", $"公开房首发失败：{ex.Message}");
        }

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            record = RoomRecordFactory.Refresh(record, key);
            try
            {
                await client.PublishAsync(record, ct).ConfigureAwait(false);
                failures = 0;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures++;
                LogWrapper.Warn("RoomHeartbeat", $"公开房心跳失败（{failures}/3）：{ex.Message}");
                if (failures >= 3) return;
            }
        }
    }
}
