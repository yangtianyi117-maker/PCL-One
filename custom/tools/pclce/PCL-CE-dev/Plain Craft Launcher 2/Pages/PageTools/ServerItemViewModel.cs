using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.Link.McPing;
using PCL.Core.UI.Hints;

namespace PCL;

/// <summary>
/// 服务器目录里的一行：把静态元数据与实时 ping 合成为界面字段。
/// 与模组条目一样，一行一个可点击的资源。
/// </summary>
public sealed partial class ServerItemViewModel : ObservableObject
{
    public ServerEntry Entry { get; }

    public ServerItemViewModel(ServerEntry entry) => Entry = entry;

    public string Name => string.IsNullOrWhiteSpace(Entry.Name) ? "未命名服务器" : Entry.Name;
    public string SubTitle => Entry.SubTitle;
    public string Description => string.IsNullOrWhiteSpace(Entry.Description) ? "（无描述）" : Entry.Description;

    public string VersionText => string.IsNullOrWhiteSpace(Entry.MinecraftVersion)
        ? "任意版本"
        : string.IsNullOrWhiteSpace(Entry.Loader) || Entry.Loader is "Vanilla" or "原版"
            ? Entry.MinecraftVersion
            : $"{Entry.MinecraftVersion} · {Entry.Loader}";

    public string SourceText => string.IsNullOrWhiteSpace(Entry.Source) ? "目录" : Entry.Source;
    public string TagsText => string.Join("  ", Entry.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)));
    public string AddressText => string.IsNullOrWhiteSpace(Entry.Java) ? Entry.Bedrock : Entry.Java;

    [ObservableProperty]
    private string _playersText = "--/--";

    [ObservableProperty]
    private string _latencyText = "-- ms";

    public string JavaAddress => Entry.Java?.Trim() ?? "";
    public string BedrockAddress => Entry.Bedrock?.Trim() ?? "";

    /// <summary>在线状态与人数、延迟都来自一次状态 ping，失败就是离线。</summary>
    public async Task RefreshStatusAsync()
    {
        if (!TrySplit(JavaAddress, out var host, out var port))
        {
            PlayersText = "--/--";
            LatencyText = "无 Java 地址";
            return;
        }

        try
        {
            using var service = McPingServiceFactory.CreateService(host, port, 6000);
            var result = await service.PingAsync().ConfigureAwait(true);
            if (result is null)
            {
                PlayersText = "--/--";
                LatencyText = "离线";
                return;
            }

            var online = result.Players?.Online ?? 0;
            var max = result.Players?.Max ?? 0;
            PlayersText = $"{online}/{max}";
            LatencyText = $"{result.Latency} ms";
        }
        catch (Exception)
        {
            PlayersText = "--/--";
            LatencyText = "离线";
        }
    }

    public void Join()
    {
        if (InstanceList.McMcInstanceSelected is not { } instance)
        {
            HintService.Show(Hints.Info("请先选择一个游戏实例"));
            return;
        }

        if (string.IsNullOrWhiteSpace(JavaAddress))
        {
            HintService.Show(Hints.Error("这台服务器没有 Java 版地址"));
            return;
        }

        GameLaunch.McLaunchStart(new GameLaunch.McLaunchOptions
        {
            instance = instance,
            ServerIp = JavaAddress
        });
    }

    public void CopyBedrock()
    {
        if (string.IsNullOrWhiteSpace(BedrockAddress))
        {
            HintService.Show(Hints.Error("这台服务器没有基岩版地址"));
            return;
        }

        try
        {
            Clipboard.SetText(BedrockAddress);
            HintService.Show(Hints.Info($"已复制基岩地址：{BedrockAddress}"));
        }
        catch (Exception ex)
        {
            HintService.Show(Hints.Error("复制失败：" + ex.Message));
        }
    }

    private static bool TrySplit(string address, out string host, out int port)
    {
        host = "";
        port = 25565;
        if (string.IsNullOrWhiteSpace(address)) return false;

        var index = address.LastIndexOf(':');
        if (index <= 0) { host = address.Trim(); return true; }

        host = address[..index].Trim();
        return int.TryParse(address[(index + 1)..], out port) && port is > 0 and < 65536;
    }
}
