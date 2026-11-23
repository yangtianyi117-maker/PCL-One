using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.Link.Bridge;
using PCL.Core.Link.Lan;
using PCL.Core.Link.Lobby;
using PCL.Core.Link.Room;
using PCL.Core.Minecraft.Profile;
using PCL.Core.ViewModel;
using PCL.Link.Protocol;

namespace PCL;

/// <summary>游戏联机页的视图模型，承载联机状态对应的显示文本与可见性。</summary>
public partial class PageToolsGameLinkViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _connectTypeText = Lang.Text("Tools.GameLink.Finish.Connecting");

    [ObservableProperty]
    private Visibility _connectTypeVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private string _connectUserNameText = "UserName";

    [ObservableProperty]
    private string _connectUserTypeText = "UserType";

    [ObservableProperty]
    private bool _createEnabled = true;

    [ObservableProperty]
    private bool _eulaStopEnabled = true;

    [ObservableProperty]
    private Visibility _finishCopyIpVisibility = Visibility.Visible;

    [ObservableProperty]
    private bool _finishExitEnabled = true;

    [ObservableProperty]
    private string _finishExitText = Lang.Text("Tools.GameLink.Finish.Exit");

    [ObservableProperty]
    private string _finishIdText = "";

    [ObservableProperty]
    private string _finishPingText = "-ms";

    [ObservableProperty]
    private Visibility _finishPingVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private string _finishQualityText = Lang.Text("Tools.GameLink.Finish.Checking");

    [ObservableProperty]
    private string _hintAnnounceText = Lang.Text("Tools.GameLink.Loading.ConnectingServer");

    [ObservableProperty]
    private Visibility _hintAnnounceVisibility = Visibility.Visible;

    [ObservableProperty]
    private bool _inputPortEnabled = true;

    [ObservableProperty]
    private string _joinLobbyId = "";

    [ObservableProperty]
    private string _loadDescText = Lang.Text("Tools.GameLink.Loading.DownloadingModule");

    [ObservableProperty]
    private bool _natTestEnabled = true;

    [ObservableProperty]
    private string _natTypeText = Lang.Text("Tools.GameLink.Nat.Test");

    [ObservableProperty]
    private bool _natayarkUserEnabled = true;

    [ObservableProperty]
    private double _natayarkUserNameOpacity = 1d;

    [ObservableProperty]
    private string _natayarkUserNameText = Lang.Text("Tools.GameLink.Natayark.Login");

    [ObservableProperty]
    private Visibility _panEulaVisibility = Visibility.Visible;

    [ObservableProperty]
    private Visibility _panFinishVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private Visibility _panSelectVisibility = Visibility.Visible;

    [ObservableProperty]
    private bool _worldListEnabled = true;

    [ObservableProperty]
    private string _worldListHint = "未发现局域网世界：先在游戏里“对局域网开放”，再点刷新；也可以手动输入端口。";

    [ObservableProperty]
    private Visibility _worldListHintVisibility = Visibility.Visible;

    public ObservableCollection<GameLinkPlayerRow> PlayerRows { get; } = [];

    public ObservableCollection<PublicRoomRow> PublicRooms { get; } = [];

    public ObservableCollection<LanRoomRow> LanRooms { get; } = [];

    public ObservableCollection<RecentRoomRow> RecentRooms { get; } = [];

    [ObservableProperty] private string _publicSearchText = "";

    [ObservableProperty] private bool _publicRoomsLoading;

    [ObservableProperty] private string _publicRoomsError = "";

    [ObservableProperty] private string _lanRoomsError = "";

    [ObservableProperty] private bool _lanRoomsLoading;

    [ObservableProperty] private string _gameUserNameHint =
        "离线模式：请确保房主和加入者的游戏用户名不同；同名会报 “That name is already taken”。";

    public bool LanRoomsAvailable => Config.Link.LanEnabled;

    public void RefreshGameUserName()
    {
        var name = ProfileService.Current?.UserName;
        GameUserNameHint = string.IsNullOrWhiteSpace(name)
            ? "当前未选择账号。离线模式请确保房主和加入者的游戏用户名不同。"
            : $"当前游戏用户名：{name}。离线模式请确保房主和加入者不同名；同名会报 “That name is already taken”。";
    }

    public void RefreshRecentRooms()
    {
        RecentRooms.Clear();
        foreach (var entry in RecentRoomsService.Load())
            RecentRooms.Add(new RecentRoomRow(entry));
    }

    public async Task RefreshLanRoomsAsync()
    {
        if (!LanRoomsAvailable)
        {
            LanRoomsError = "局域网发现已在设置中关闭";
            return;
        }

        LanRoomsLoading = true;
        LanRoomsError = "";
        try
        {
            var port = Math.Clamp(Config.Link.LanPort, 1, 65535);
            var discovery = new LanDiscovery { ProbePort = port };
            var rooms = await discovery.ProbeOnceAsync().ConfigureAwait(true);
            LanRooms.Clear();
            foreach (var room in rooms)
                LanRooms.Add(new LanRoomRow(room));
            if (LanRooms.Count == 0)
                LanRoomsError = "没有发现局域网房间：确认对方已对局域网开放，且在同一网段";
        }
        catch (Exception ex)
        {
            Common.Log(ex, "[LAN] 局域网房间探测失败");
            LanRoomsError = "局域网探测失败，请稍后重试";
        }
        finally
        {
            LanRoomsLoading = false;
        }
    }

    public bool PublicRoomsAvailable => RoomDirectoryFactory.IsAvailable;

    public async Task RefreshPublicRoomsAsync()
    {
        if (!PublicRoomsAvailable)
        {
            PublicRoomsError = "未配置公开房索引";
            return;
        }

        PublicRoomsLoading = true;
        PublicRoomsError = "";
        var client = RoomDirectoryFactory.TryCreate();
        if (client is null) return;
        try
        {
            var rooms = await client.SearchAsync(new RoomFilter
            {
                Query = PublicSearchText,
                Limit = 50
            }).ConfigureAwait(true);

            PublicRooms.Clear();
            foreach (var room in rooms.GroupBy(room => room.RoomId).Select(group => group.First()))
                PublicRooms.Add(new PublicRoomRow(room));
            if (PublicRooms.Count == 0)
                PublicRoomsError = "暂时没有公开房间";
        }
        catch (Exception ex)
        {
            // PCL 定制版：公开房失败不弹/不显示错误，只写日志。
            Common.Log(ex, "[Link] 公开房列表加载失败");
            PublicRoomsError = "";
        }
        finally
        {
            client.Dispose();
            PublicRoomsLoading = false;
        }
    }

    /// <summary>按当前子页面更新三个子页面的可见性。</summary>
    public void ApplySubpage(PageToolsGameLink.Subpages subpage)
    {
        PanEulaVisibility = subpage == PageToolsGameLink.Subpages.PanEula ? Visibility.Visible : Visibility.Collapsed;
        PanSelectVisibility = subpage == PageToolsGameLink.Subpages.PanSelect ? Visibility.Visible : Visibility.Collapsed;
        PanFinishVisibility = subpage == PageToolsGameLink.Subpages.PanFinish ? Visibility.Visible : Visibility.Collapsed;
    }

    [RelayCommand]
    private void PasteLobbyId()
    {
        string lobbyId;
        try
        {
            lobbyId = Clipboard.GetText(TextDataFormat.Text);
        }
        catch (Exception ex)
        {
            Common.Log(ex, "从剪贴板识别大厅编号出错");
            return;
        }

        if (!string.IsNullOrEmpty(lobbyId))
            JoinLobbyId = lobbyId;
        else
            HintService.Show(Hints.Info(Lang.Text("Tools.GameLink.Join.InvalidText")));
    }

    [RelayCommand]
    private void ClearLobbyId()
    {
        JoinLobbyId = string.Empty;
    }

    // ---- 服务器目录（像模组列表一样浏览服务器） ----

    /// <summary>服务器目录行。第一性：一台服务器就是一行资源，字段与模组条目同构。</summary>
    public ObservableCollection<ServerItemViewModel> ServerItems { get; } = [];

    [ObservableProperty]
    private string _serverListError = "";

    [ObservableProperty]
    private bool _serverListLoading;

    // ---- 手机进服（固定服务器，Bedrock 通过 Geyser） ----

    /// <summary>手机直接填进 Minecraft 的基岩地址。</summary>
    public string OfficialBedrockAddress => Config.Link.ServerBedrock;

    /// <summary>PC 端 Java 地址，同服同世界。</summary>
    public string OfficialJavaAddress => Config.Link.ServerJava;

    [ObservableProperty]
    private string _officialBedrockStatus = "检测中…";

    [ObservableProperty]
    private string _officialBedrockDetail = "";

    [ObservableProperty]
    private bool _officialBedrockLoading;

    public async Task RefreshOfficialAsync()
    {
        if (OfficialBedrockLoading) return;
        OfficialBedrockLoading = true;
        OfficialBedrockStatus = "检测中…";
        try
        {
            var address = OfficialBedrockAddress?.Trim() ?? "";
            if (!TrySplitAddress(address, out var host, out var port))
            {
                OfficialBedrockStatus = "地址未配置";
                OfficialBedrockDetail = "请检查 设置 → 联机 → 固定服务器";
                return;
            }

            var result = await PCL.Core.Link.Bedrock.BedrockPing
                .PingAsync(host, port, 3000)
                .ConfigureAwait(true);

            if (result is null)
            {
                OfficialBedrockStatus = "离线 / 无响应";
                OfficialBedrockDetail = $"手机仍可尝试进入 {address}";
                return;
            }

            OfficialBedrockStatus = $"在线 · {result.Players}/{result.MaxPlayers} 人 · {result.LatencyMs} ms";
            OfficialBedrockDetail = string.IsNullOrWhiteSpace(result.Version)
                ? address
                : $"{address} · Bedrock {result.Version}" +
                  (string.IsNullOrWhiteSpace(result.Motd) ? "" : $" · {result.Motd}");
        }
        catch (Exception ex)
        {
            Common.Log(ex, "[Bedrock] 固定服务器状态查询失败");
            OfficialBedrockStatus = "离线 / 无响应";
            OfficialBedrockDetail = OfficialBedrockAddress;
        }
        finally
        {
            OfficialBedrockLoading = false;
        }
    }

    private static bool TrySplitAddress(string address, out string host, out int port)
    {
        host = "";
        port = 25565;
        if (string.IsNullOrWhiteSpace(address)) return false;
        var index = address.LastIndexOf(':');
        if (index <= 0)
        {
            host = address.Trim();
            return host.Length > 0;
        }

        host = address[..index].Trim();
        return host.Length > 0 && int.TryParse(address[(index + 1)..], out port) && port is > 0 and < 65536;
    }

    // ---- 同 Wi-Fi 基岩桥 ----

    [ObservableProperty]
    private string _bridgeStatusText = "未启动";

    [ObservableProperty]
    private string _bridgeAddress = "";

    [ObservableProperty]
    private bool _bridgeRunning;

    [ObservableProperty]
    private bool _bridgeBusy;

    public string BridgePortText => $"0.0.0.0:{Math.Clamp(Config.Link.BedrockBridgePort, 1, 65535)}";

    public void RefreshBridge()
    {
        var snapshot = ProBedrockBridgeService.Current;
        BridgeRunning = snapshot.State is BedrockBridgeState.WaitingForWorld
            or BedrockBridgeState.Starting or BedrockBridgeState.Running or BedrockBridgeState.Downloading;
        BridgeBusy = snapshot.State is BedrockBridgeState.Downloading or BedrockBridgeState.Starting;
        BridgeStatusText = snapshot.State switch
        {
            BedrockBridgeState.Stopped => "未启动",
            BedrockBridgeState.Downloading => snapshot.Message,
            BedrockBridgeState.WaitingForWorld => snapshot.Message,
            BedrockBridgeState.Starting => snapshot.Message,
            BedrockBridgeState.Running => string.IsNullOrWhiteSpace(snapshot.Address)
                ? "已启动"
                : $"已启动：手机进入 {snapshot.Address}",
            BedrockBridgeState.Error => snapshot.Message,
            _ => snapshot.Message
        };
        BridgeAddress = snapshot.Address;
    }

    public async Task StartBridgeAsync()
    {
        await ProBedrockBridgeService.StartAsync(Math.Clamp(Config.Link.BedrockBridgePort, 1, 65535))
            .ConfigureAwait(true);
        RefreshBridge();
    }

    public void StopBridge()
    {
        ProBedrockBridgeService.Stop();
        RefreshBridge();
    }

    public async Task RefreshServersAsync()
    {
        if (ServerListLoading) return;
        ServerListLoading = true;
        ServerListError = "";
        try
        {
            var entries = await ServerDirectory.LoadAsync().ConfigureAwait(true);
            ServerItems.Clear();
            foreach (var entry in entries)
                ServerItems.Add(new ServerItemViewModel(entry));

            foreach (var row in ServerItems)
                _ = row.RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            ServerListError = "服务器目录加载失败：" + ex.Message;
        }
        finally
        {
            ServerListLoading = false;
        }
    }
}

/// <summary>公开房列表行。</summary>
public sealed class PublicRoomRow(RoomRecord record){
    public RoomRecord Record { get; } = record;

    public string Title => $"{Record.Name} · {Record.MinecraftVersion}";

    public string Info
    {
        get
        {
            var loader = string.IsNullOrWhiteSpace(Record.Loader) || Record.Loader == "vanilla"
                ? "原版"
                : $"{Record.Loader} {Record.LoaderVersion}";
            var tags = Record.Tags.Count == 0 ? "" : " · " + string.Join('/', Record.Tags);
            var interop = Record.Loader == "vanilla" &&
                          ViaProxyVersionTable.IsSupported(Record.MinecraftVersion)
                ? "可跨版本"
                : "仅同版本";
            return $"{loader} · {interop} · {Record.Players}/{Record.MaxPlayers} 人 · 房主 {Record.HostName}{tags}";
        }
    }
}

/// <summary>局域网房间列表行。</summary>
public sealed class LanRoomRow(LanRoom room){
    public LanRoom Room { get; } = room;

    public string Title => $"{Room.Name} · {Room.Address}";

    public string Info => Room.Official
        ? "官方房间 · 双击直接进入"
        : "局域网房间 · 双击直接进入";
}

/// <summary>最近房间列表行。</summary>
public sealed class RecentRoomRow(RecentRoomEntry entry)
{
    public RecentRoomEntry Entry { get; } = entry;

    public string Title => Entry.Code;

    public string Info => Entry.Source == "legacy"
        ? $"旧版记录 · {Entry.JoinedAt:MM-dd HH:mm}"
        : $"{Entry.JoinedAt:MM-dd HH:mm} · 双击填入";
}

/// <summary>游戏联机页的玩家列表行。</summary>
public sealed class GameLinkPlayerRow
{
    public required string Name { get; init; }

    public required string Info { get; init; }

    public required string Vendor { get; init; }

    public required string MachineId { get; init; }
}
