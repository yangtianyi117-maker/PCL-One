using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Link.McPing;
using PCL.Core.Link.Natayark;
using PCL.Core.Link.Pack;
using PCL.Core.Link.Scaffolding;
using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Link.Scaffolding.Client.Requests;
using PCL.Core.Link.Scaffolding.EasyTier;
using PCL.Core.Logging;
using PCL.Core.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App.IoC;
using PCL.Core.Platform;
using PCL.Core.UI;
using PCL.Core.Link.McPing;

namespace PCL.Core.Link.Lobby;

/// <summary>
/// Lobby server. For auto-management
/// </summary>
[LifecycleService(LifecycleState.Loaded)]
public class LobbyService() : GeneralService("lobby", "LobbyService")
{
    private static Task? _discoveringTask;
    private static CancellationTokenSource _discoveringCts = new();

    private static readonly Timer _ServerGameWatcher =
        new(_CheckGameState, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));

    private static bool _isGameWatcherRunnable = false;
    private static int _isLeaving;
    private static volatile bool _isJoining;

    /// <summary>
    /// 当前大厅会话。状态字段统一收口在这里。
    /// </summary>
    public static LobbySession Current { get; } = new();

    /// <summary>
    /// Current lobby state.
    /// </summary>
    public static LobbyState CurrentState => Current.State;


    /// <summary>
    /// Founded local Minecraft worlds.
    /// </summary>
    public static ObservableCollection<FoundWorld> DiscoveredWorlds { get; } = [];

    /// <summary>LAN 发现时使用的协议号；由 UI 按当前实例版本设置。</summary>
    public static int PreferredProtocolVersion { get; set; } = 772;

    /// <summary>
    /// Current players in current lobby.
    /// </summary>
    public static ObservableCollection<PlayerProfile> Players => Current.Players;

    /// <summary>
    /// Demonstrate whether the current user is the host of the lobby.
    /// </summary>
    public static bool IsHost => Current.IsHost;

    /// <summary>
    /// Current lobby full code.
    /// </summary>
    public static string? CurrentLobbyCode => Current.LobbyCode;

    /// <summary>
    /// Current lobby username.
    /// </summary>
    public static string? CurrentUserName => Current.UserName;

    /// <summary>
    /// 房主发布的内容清单；加入成功后自动拉取。null 表示该房间不提供零整合包联机。
    /// </summary>
    public static PackManifest? CurrentPackManifest => Current.PackManifest;

    #region UI Events

    /// <summary>
    /// Invoked when lobby state changed. (first arg is the old state; second arg is the new state.)
    /// </summary>
    public static event Action<LobbyState, LobbyState>? StateChanged;

    /// <summary>
    /// Invoked when need to download EasyTier core files.
    /// </summary>
    public static event Action? OnNeedDownloadEasyTier;

    /// <summary>
    /// Invoked when user stop the game in server mode.
    /// </summary>
    public static event Action? OnUserStopGame;

    /// <summary>
    /// Invoked when client ping happened.
    /// </summary>
    public static event Action<long>? OnClientPing;

    /// <summary>
    /// Invoked when server shut down.
    /// </summary>
    public static event Action? OnServerShutDown;


    /// <summary>
    /// Invoked when server started successfully.
    /// </summary>
    public static event Action? OnServerStarted;

    public static event Action<Exception>? OnServerException;

    #endregion

    /// <inheritdoc />
    public override void Stop()
    {
        Current.Cancellation.Cancel();
        Current.Controller.CloseAsync().GetAwaiter().GetResult();
        _ServerGameWatcher.Dispose();
        Current.Cancellation.Dispose();

        _discoveringCts.Cancel();
        if (_discoveringTask is not null)
        {
            Task.WhenAll(_discoveringTask);
            _discoveringTask.Dispose();
        }

        _discoveringCts.Dispose();
    }

    private static bool _IsEasyTierCoreFileNotExist() =>
        !File.Exists(Path.Combine(EasyTierMetadata.EasyTierFilePath, "easytier-core.exe")) &&
        !File.Exists(Path.Combine(EasyTierMetadata.EasyTierFilePath, "Packet.dll")) &&
        !File.Exists(Path.Combine(EasyTierMetadata.EasyTierFilePath, "easytier-cli.exe"));


    public static async Task InitializeAsync()
    {
        if (CurrentState is not LobbyState.Idle && CurrentState is not LobbyState.Error)
        {
            return;
        }

        _SetState(LobbyState.Initializing);
        try
        {
            if (_IsEasyTierCoreFileNotExist())
            {
                LogWrapper.Info("LobbyService", "EasyTier not found, starting download.");
                OnNeedDownloadEasyTier?.Invoke();
            }
            else
            {
                LogWrapper.Info("LobbyService", "EasyTier files check completed.");
            }

            // refresh naid token
            var naidRefreshToken = States.Link.NaidRefreshToken;
            if (!string.IsNullOrWhiteSpace(naidRefreshToken))
            {
                var expTime = States.Link.NaidRefreshExpireTime;
                if (!string.IsNullOrWhiteSpace(expTime) &&
                    Convert.ToDateTime(expTime).CompareTo(DateTime.Now) < 0)
                {
                    States.Link.NaidRefreshToken = string.Empty;
                    HintWrapper.Show(Lang.Text("Tools.GameLink.Natayark.TokenExpired"), HintTheme.Error);
                }
                else
                {
                    await NatayarkProfileManager.GetNaidDataAsync(naidRefreshToken, true).ConfigureAwait(false);
                }
            }

            _SetState(LobbyState.Initialized);
            LogWrapper.Info("LobbyService", "Lobby service initialized successfully.");

            _ = DiscoverWorldAsync();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "LobbyService", "Lobby service initialization failed.");
            HintWrapper.Show(Lang.Text("Link.Lobby.InitFailed"), HintTheme.Error);
            _SetState(LobbyState.Error);
        }
    }

    /// <summary>
    /// Discover minecraft shared world.
    /// </summary>
    public static async Task DiscoverWorldAsync()
    {
        if (_discoveringCts.IsCancellationRequested)
        {
            return;
        }

        if (CurrentState is not LobbyState.Initialized && CurrentState is not LobbyState.Idle)
        {
            return;
        }

        _SetState(LobbyState.Discovering);
        await _RunInUiAsync(() => DiscoveredWorlds.Clear()).ConfigureAwait(false);

        _discoveringTask = Task.Run(async () =>
        {
            var recordedPorts = new ConcurrentSet<int>();
            using var listener = new BroadcastListener();

            var handler = new Action<BroadcastRecord, IPEndPoint>((info, _) => Task.Run(async () =>
            {
                if (!recordedPorts.TryAdd(info.Address.Port)) return;

                using var pinger = McPingServiceFactory.CreateService(new IPEndPoint(IPAddress.Loopback, info.Address.Port));
                if (pinger is McPingService mcPing)
                {
                    mcPing.ProtocolVersion = PreferredProtocolVersion;
                }
                using var cts = new CancellationTokenSource(2000);

                try
                {
                    var pingRes = await pinger.PingAsync(cts.Token).ConfigureAwait(false);

                    if (pingRes is null)
                    {
                        throw new ArgumentNullException(nameof(pingRes), "Failed to ping minecraft entity.");
                    }

                    var worldName = Lang.Text("Link.Lobby.WorldNameFormat",
                        pingRes.Description,
                        pingRes.Version.Name,
                        info.Address.Port);
                    await _RunInUiAsync(() => DiscoveredWorlds.Add(new FoundWorld(worldName, info.Address.Port)))
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogWrapper.Error(ex, "LobbyService", $"Pinging port {info.Address.Port} failed.");
                }
            }));

            listener.OnReceive += handler;
            listener.Start();
            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            listener.OnReceive -= handler;
        }, _discoveringCts.Token);

        _SetState(LobbyState.Initialized);
    }

    private static bool _NotHaveNaid() =>
        LobbyInfoProvider.RequiresLogin &&
        string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.AccessToken);

    /// <summary>
    /// Create a new lobby.
    /// </summary>
    /// <param name="port">Minecraft share port.</param>
    /// <param name="username">Player name.</param>
    /// <param name="packManifest">Optional pack manifest published to joiners.</param>
    public static async Task<bool> CreateLobbyAsync(
        int port,
        string username,
        PackManifest? packManifest = null,
        PCL.Core.Link.Room.RoomPublishOptions? publish = null)
    {
        if (_NotHaveNaid())
        {
            HintWrapper.Show(Lang.Text("Link.Lobby.LoginRequired"), HintTheme.Error);
            return false;
        }

        await _discoveringCts.CancelAsync().ConfigureAwait(false);

        _SetState(LobbyState.Creating);
        try
        {
            Current.UserName = username;
            Current.IsHost = true;

            var serverEntity = await Current.Controller
                .LaunchServerAsync(username, port, packManifest)
                .ConfigureAwait(false);
            if (serverEntity is null)
            {
                HintWrapper.Show(Lang.Text("Link.Lobby.CreateRoomFailed"), HintTheme.Error);
                return false;
            }

            Current.LobbyCode = serverEntity.EasyTier.Lobby.FullCode;
            Current.PackManifest = packManifest;
            PCL.Core.Link.Room.RecentRoomsService.Add(CurrentLobbyCode, "host");

            if (publish is { IsPublic: true } && packManifest is not null && CurrentLobbyCode is not null)
            {
                try
                {
                    var key = PackHostKeyStore.GetOrCreate();
                    var record = PCL.Core.Link.Room.RoomRecordFactory.Create(
                        CurrentLobbyCode, packManifest, username, 1, publish, key);
                    await PCL.Core.Link.Room.RoomHeartbeatService
                        .StartAsync(record, key).ConfigureAwait(false);
                }
                catch (Exception publishEx)
                {
                    LogWrapper.Warn("LobbyService", $"发布公开房失败：{publishEx.Message}");
                }
            }

            serverEntity.Server.ServerStopped += () => OnServerShutDown?.Invoke();
            serverEntity.Server.PlayerProfilePing += _ServerOnPlayerPing;
            serverEntity.Server.ServerStarted += _ServerOnServerStarted;
            serverEntity.Server.ServerException += _ServerOnServerException;
            //serverEntity.EasyTier.EasyTierProcessExisted += () =>
            //{
            //    OnHint?.Invoke("EasyTierCore异常退出", CoreHintType.Critical);
            //    OnServerShutDown?.Invoke();
            //}; this code will be invoked when EasyTier process successfully exited, not in failed state.

            serverEntity.Server.Start();

            _SetState(LobbyState.Connected);
            _isGameWatcherRunnable = true;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "LobbyService", "Failed to create lobby.");
            HintWrapper.Show(Lang.Text("Link.Lobby.CreateLobbyFailed"), HintTheme.Error);
            await LeaveLobbyAsync().ConfigureAwait(false);

            return false;
        }

        return true;
    }

    private static void _ServerOnServerException(Exception? ex)
    {
        if (ex is null)
        {
            return;
        }

        OnServerException?.Invoke(ex);
    }

    private static void _ServerOnServerStarted(IReadOnlyList<PlayerProfile> profiles)
    {
        LogWrapper.Debug("LobbyService", "Send server started event.");
        OnServerStarted?.Invoke();
        _ServerOnPlayerPing(profiles);
    }

    private static void _ServerOnPlayerPing(IReadOnlyList<PlayerProfile> players)
    {
        _ = _RunInUiAsync(() =>
        {
            var currentMachineIds = new HashSet<string>(Players.Select(p => p.MachineId));
            var newMachineIds = new HashSet<string>(players.Select(p => p.MachineId));
            
            if (currentMachineIds.SetEquals(newMachineIds))
            {
                LogWrapper.Debug("Player list has not changed");
                return; // nothing was changed
            }

            LogWrapper.Debug("LobbyService", "Player list membership has changed, updating UI.");

            var sortedNewPlayers = PlayerListHandler.Sort(players);

            var idsToRemove = currentMachineIds.Except(newMachineIds).ToList();
            if (idsToRemove.Any())
            {
                var playersToRemove = Players.Where(p => idsToRemove.Contains(p.MachineId)).ToList();
                foreach (var player in playersToRemove)
                {
                    Players.Remove(player);
                }
            }

            var idsToAdd = newMachineIds.Except(currentMachineIds).ToList();
            if (idsToAdd.Any())
            {
                var playersToAdd = sortedNewPlayers.Where(p => idsToAdd.Contains(p.MachineId)).ToList();
                foreach (var player in playersToAdd)
                {
                    Players.Add(player);
                }
            }
        });
    }

    /// <summary>
    /// Join an exist lobby.
    /// </summary>
    /// <param name="lobbyCode">Lobby share code.</param>
    /// <param name="username">Current use name.</param>
    public static async Task<bool> JoinLobbyAsync(string lobbyCode, string username)
    {
        await _discoveringCts.CancelAsync().ConfigureAwait(false);

        _isJoining = true;
        _SetState(LobbyState.Joining);

        LogWrapper.Info("LobbyService", $"Try to join lobby {lobbyCode}");

        try
        {
            Current.UserName = username;
            Current.LobbyCode = lobbyCode;
            Current.IsHost = false;

            var clientEntity = await Current.Controller.LaunchClientAsync(username, lobbyCode, Current.Cancellation.Token).ConfigureAwait(false);

            if (clientEntity is null)
            {
                throw new InvalidOperationException(
                    Lang.Text("Link.Lobby.JoinFailed"));
            }

            clientEntity.Client.Heartbeat += _ClientOnHeartbeat;
            clientEntity.Client.ServerShuttedDown += _ClientOnServerShutDown;

            Current.PackManifest = null;
            try
            {
                using var manifestCts = CancellationTokenSource.CreateLinkedTokenSource(Current.Cancellation.Token);
                manifestCts.CancelAfter(TimeSpan.FromSeconds(5));
                Current.PackManifest = await clientEntity.Client
                    .SendRequestAsync(new GetPackManifestRequest(), manifestCts.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception manifestEx)
            {
                // 老版本房主没有 c:pack_manifest 处理器；这不影响原版联机。
                LogWrapper.Warn("LobbyService", $"获取房间内容清单失败：{manifestEx.Message}");
            }

            _SetState(LobbyState.Connected);
            PCL.Core.Link.Room.RecentRoomsService.Add(CurrentLobbyCode, "join");
        }
        catch (ArgumentException codeEx)
        {
            _isJoining = false;
            LogWrapper.Error(codeEx, "LobbyService", $"Failed to join lobby {lobbyCode}.");
            HintWrapper.Show(Lang.Text("Link.Lobby.InvalidCodeFormat"), HintTheme.Error);
            if (CurrentState is not LobbyState.Leaving)
                await LeaveLobbyAsync().ConfigureAwait(false);

            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _isJoining = false;

            // 用户点了“退出大厅”或连接被取消：这是预期路径，不再弹错误/重复清理。
            if (CurrentState is LobbyState.Leaving || Current.Cancellation.IsCancellationRequested ||
                ex is OperationCanceledException)
            {
                LogWrapper.Info("LobbyService", "加入大厅已取消");
                return false;
            }

            LogWrapper.Error(ex, "LobbyService", $"Failed to join lobby {lobbyCode}.");
            var message = ex is IOException or System.Net.Sockets.SocketException
                ? Lang.Text("Link.Lobby.JoinFailed")
                : ex.Message;
            HintWrapper.Show(message, HintTheme.Error);
            if (CurrentState is not LobbyState.Leaving)
                await LeaveLobbyAsync().ConfigureAwait(false);

            return false;
        }
        finally
        {
            _isJoining = false;
        }

        return true;
    }

    private static void _ClientOnServerShutDown()
    {
        OnServerShutDown?.Invoke();
    }

    private static void _ClientOnHeartbeat(IReadOnlyList<PlayerProfile> players, long latency)
    {
        _ = _RunInUiAsync(() =>
        {
            var currentMachineIds = new HashSet<string>(Players.Select(p => p.MachineId));
            var newMachineIds = new HashSet<string>(players.Select(p => p.MachineId));

            if (currentMachineIds.SetEquals(newMachineIds))
            {
                return; // nothing was changed
            }

            LogWrapper.Debug("LobbyService", "Player list membership has changed, updating UI.");

            var sortedNewPlayers = PlayerListHandler.Sort(players);

            var idsToRemove = currentMachineIds.Except(newMachineIds).ToList();
            if (idsToRemove.Any())
            {
                var playersToRemove = Players.Where(p => idsToRemove.Contains(p.MachineId)).ToList();
                foreach (var player in playersToRemove)
                {
                    Players.Remove(player);
                }
            }

            var idsToAdd = newMachineIds.Except(currentMachineIds).ToList();
            if (idsToAdd.Any())
            {
                var playersToAdd = sortedNewPlayers.Where(p => idsToAdd.Contains(p.MachineId)).ToList();
                foreach (var player in playersToAdd)
                {
                    Players.Add(player);
                }
            }

            OnClientPing?.Invoke(latency);
        });
    }


    /// <summary>
    /// Leave from lobby.
    /// </summary>
    public static async Task LeaveLobbyAsync()
    {
        if (Interlocked.Exchange(ref _isLeaving, 1) == 1)
            return;

        try
        {
            _SetState(LobbyState.Leaving);

            await Current.Cancellation.CancelAsync().ConfigureAwait(false);

            // 等待正在进行的加入流程退出，避免在 ConnectAsync 读包时 Dispose 客户端，
            // 导致 “远程主机强迫关闭了一个现有的连接” 的级联报错。
            for (var i = 0; i < 50 && _isJoining; i++)
                await Task.Delay(100).ConfigureAwait(false);

            await PCL.Core.Link.Room.RoomHeartbeatService.StopAsync().ConfigureAwait(false);
            await PCL.Core.Link.Bridge.ViaProxyBridgeManager.StopAsync().ConfigureAwait(false);

            Players.Clear();
            Current.LobbyCode = null;
            Current.UserName = null;
            Current.PackManifest = null;
            Current.IsHost = false;

            if (Current.Controller.ScfClientEntity?.Client is not null)
            {
                Current.Controller.ScfClientEntity.Client.Heartbeat -= _ClientOnHeartbeat;
            }

            if (Current.Controller.ScfServerEntity?.Server is not null)
            {
                Current.Controller.ScfServerEntity.Server.PlayerProfilePing -= _ServerOnPlayerPing;
                Current.Controller.ScfServerEntity.Server.ServerStarted -= _ServerOnServerStarted;
            }

            await Current.Controller.CloseAsync().ConfigureAwait(false);


            Current.Cancellation = new CancellationTokenSource();
            _SetState(LobbyState.Initialized);

            LogWrapper.Info("LobbyService", "Left lobby and cleaned up resources.");

            _isGameWatcherRunnable = false;

            _discoveringCts = new CancellationTokenSource();
            _ = DiscoverWorldAsync();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "LobbyService", "Failed when leave lobby.");
        }
        finally
        {
            _isLeaving = 0;
        }
    }

    private static void _SetState(LobbyState newState)
    {
        var oldState = CurrentState;
        if (oldState == newState)
        {
            return;
        }

        Current.State = newState;

        LogWrapper.Info("LobbyService", $"Lobby state changed from {oldState} to {newState}");

        StateChanged?.Invoke(oldState, newState);
    }

    private static void _CheckGameState(object? state)
    {
        if (!_isGameWatcherRunnable)
        {
            return;
        }

        if (Current.Controller.ScfServerEntity is null)
        {
            return;
        }

        LobbyController.IsHostInstanceAvailableAsync(Current.Controller.ScfServerEntity.EasyTier.MinecraftPort)
            .ContinueWith(async (task) =>
            {
                var isExist = await task.ConfigureAwait(false);
                if (!isExist)
                {
                    _isGameWatcherRunnable = false;
                    OnUserStopGame?.Invoke();
                }
            });
    }

    private static async Task _RunInUiAsync(Action action)
    {
        if (PlatformServices.UiDispatcher is { } dispatcher)
            await dispatcher.InvokeAsync(action);
    }
}

/// <summary>
/// Founded minecraft world information.
/// </summary>
/// <param name="Name">World name.</param>
/// <param name="Port">World share port.</param>
public record FoundWorld(string Name, int Port);