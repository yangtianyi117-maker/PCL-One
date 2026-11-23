using System.Threading.Tasks;

namespace PCL.Core.Link.Bridge;

/// <summary>
/// 全局只保留一个加入者翻译代理；离开房间/开始新代理时关闭旧进程。
/// </summary>
public static class ViaProxyBridgeManager
{
    private static readonly object Lock = new();
    private static ViaProxyBridge? _current;

    public static async Task<ViaProxyBridge> StartAsync(
        string jarPath, string javaExePath, string hostVersion, int targetPort)
    {
        await StopAsync().ConfigureAwait(false);
        var bridge = await ViaProxyBridge
            .StartAsync(jarPath, javaExePath, hostVersion, targetPort)
            .ConfigureAwait(false);
        lock (Lock) _current = bridge;
        return bridge;
    }

    public static async Task StopAsync()
    {
        ViaProxyBridge? bridge;
        lock (Lock)
        {
            bridge = _current;
            _current = null;
        }

        if (bridge is not null)
            await bridge.StopAsync().ConfigureAwait(false);
    }
}
