using System.Collections.ObjectModel;
using System.Threading;
using PCL.Core.Link.Pack;
using PCL.Core.Link.Scaffolding.Client.Models;

namespace PCL.Core.Link.Lobby;

/// <summary>
/// 一次大厅会话的完整状态。LobbyService 只保留一个 Current 实例，
/// 所有房间字段、控制器和取消令牌都从这里读写，UI 通过 LobbyService 的兼容属性访问。
/// </summary>
public sealed class LobbySession
{
    public LobbyState State { get; internal set; } = LobbyState.Idle;
    public string? LobbyCode { get; internal set; }
    public string? UserName { get; internal set; }
    public PackManifest? PackManifest { get; internal set; }
    public bool IsHost { get; internal set; }
    public ObservableCollection<PlayerProfile> Players { get; } = [];
    public LobbyController Controller { get; } = new();
    public CancellationTokenSource Cancellation { get; internal set; } = new();
}
