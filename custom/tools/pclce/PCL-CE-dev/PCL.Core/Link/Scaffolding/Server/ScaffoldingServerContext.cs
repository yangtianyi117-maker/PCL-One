using PCL.Core.Link.Pack;
using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Link.Scaffolding.EasyTier;
using PCL.Core.Link.Scaffolding.Server.Abstractions;
using PCL.Core.App;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PCL.Core.Link.Scaffolding.Server;

/// <summary>
/// Scaffolding Server running context.
/// </summary>
public class ScaffoldingServerContext : IServerContext
{
    private readonly ConcurrentDictionary<string, TrackedPlayerProfile> _trackedPlayers = [];

    /// <inheritdoc />
    public ConcurrentDictionary<string, TrackedPlayerProfile> TrackedPlayers
    {
        get => _trackedPlayers;
        private init => _trackedPlayers = value;
    }

    public IReadOnlyList<PlayerProfile> PlayerProfiles =>
        _trackedPlayers.Values.Select(player => player.Profile).ToList().AsReadOnly();

    /// <inheritdoc />
    public event Action<IReadOnlyList<PlayerProfile>>? PlayerProfilesPing;

    public void OnPlayerProfilesChanged()
    {
        var currentProfiles = PlayerProfiles;
        Task.Run(() => PlayerProfilesPing?.Invoke(currentProfiles));
    }

    /// <inheritdoc />
    public int MinecraftServerProt { get; }

    /// <inheritdoc />
    public LobbyInfo UserLobbyInfo { get; }

    /// <inheritdoc />
    public string PlayerName { get; }

    /// <inheritdoc />
    public PackManifest? PackManifest { get; }

    private ScaffoldingServerContext(
        ConcurrentDictionary<string, TrackedPlayerProfile> profiles,
        int mcPort,
        LobbyInfo info,
        string playerName,
        PackManifest? packManifest)
    {
        TrackedPlayers = profiles;
        MinecraftServerProt = mcPort;
        UserLobbyInfo = info;
        PlayerName = playerName;
        PackManifest = packManifest;
    }

    /// <summary>
    /// Create a <see cref="ScaffoldingServerContext"/>.
    /// </summary>
    /// <param name="playerName">Player name.</param>
    /// <param name="mcPort">Minecraft shared port.</param>
    /// <param name="packManifest">可选的内容清单；为 null 时不启用零整合包联机。</param>
    public static ScaffoldingServerContext Create(
        string playerName, int mcPort, PackManifest? packManifest = null)
    {
        var machineId = Utils.Secret.Identify.LauncherId;
        var profile = new PlayerProfile
        {
            Name = playerName,
            MachineId = Utils.Secret.Identify.LauncherId,
            // Please update ScaffoldingFactory.cs at the same time.
            Vendor = $"PCL CE {Basics.VersionName}, EasyTier {EasyTierMetadata.CurrentEasyTierVer}",
            Kind = PlayerKind.HOST
        };

        var tracked = new TrackedPlayerProfile { Profile = profile, LastSeenUtc = DateTime.UtcNow };

        var roomCode = PCL.Core.Link.Room.LobbyCodeService.Generate();
        var lobbyInfo = new LobbyInfo(roomCode.FullCode, roomCode.NetworkName, roomCode.NetworkSecret);

        var profiles = new ConcurrentDictionary<string, TrackedPlayerProfile>();
        profiles.TryAdd(machineId, tracked);

        return new ScaffoldingServerContext(profiles, mcPort, lobbyInfo, playerName, packManifest);
    }
}