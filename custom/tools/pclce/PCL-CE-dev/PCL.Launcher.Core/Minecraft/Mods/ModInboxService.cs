using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PCL.Core.App;

namespace PCL;

/// <summary>
/// PCL 定制版 的“拖 jar 即装”入口：把 jar 放进 PCL\ModInbox，
/// 自动读取元数据并按当前实例的 MC 版本/加载器安装到 mods。
///
/// 这是业务逻辑，放在 PCL.Launcher.Core，UI 只负责启动/停止和打开目录。
/// </summary>
public static class ModInboxService
{
    private static readonly object Lock = new();
    private static FileSystemWatcher? _watcher;

    public static string InboxPath => Path.Combine(Paths.Data, "ModInbox");
    public static string DonePath => Path.Combine(InboxPath, "done");
    public static string FailedPath => Path.Combine(InboxPath, "failed");

    public static void Start()
    {
        lock (Lock)
        {
            if (_watcher is not null) return;
            Directory.CreateDirectory(InboxPath);
            Directory.CreateDirectory(DonePath);
            Directory.CreateDirectory(FailedPath);

            _watcher = new FileSystemWatcher(InboxPath, "*.jar")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            _watcher.Created += (_, e) => _ = HandleAsync(e.FullPath);
            _watcher.Renamed += (_, e) => _ = HandleAsync(e.FullPath);
            LauncherCommon.Log($"[ModInbox] 已监听：{InboxPath}");
        }
    }

    public static void Stop()
    {
        lock (Lock)
        {
            _watcher?.Dispose();
            _watcher = null;
        }
    }

    private static async Task HandleAsync(string jarPath)
    {
        try
        {
            if (!File.Exists(jarPath) ||
                !string.Equals(Path.GetExtension(jarPath), ".jar", StringComparison.OrdinalIgnoreCase))
                return;

            // 等文件写完
            await Task.Delay(800).ConfigureAwait(false);

            var instance = InstanceList.McMcInstanceSelected;
            if (instance is null)
            {
                LauncherCommon.Log("[ModInbox] 没有选中实例，暂不安装：" + jarPath);
                return;
            }

            var loader = instance.Info.HasNeoForge ? "NeoForge"
                : instance.Info.HasForge ? "Forge"
                : instance.Info.HasFabric ? "Fabric"
                : instance.Info.HasQuilt ? "Quilt"
                : "vanilla";

            var results = await ModJarInstaller
                .InstallAsync([jarPath], instance.PathIndie, instance.Info.VanillaName, loader)
                .ConfigureAwait(false);

            var result = results.FirstOrDefault();
            if (result is { Success: true, Info: not null } && result.Info.Dependencies.Count > 0)
            {
                using var resolver = new ModrinthDependencyResolver();
                var dependencies = await resolver
                    .InstallDependenciesAsync(result.Info, instance.PathIndie,
                        instance.Info.VanillaName, loader)
                    .ConfigureAwait(false);
                foreach (var dependency in dependencies)
                    LauncherCommon.Log($"[ModInbox] {dependency.Message}");
            }

            var target = result is { Success: true } ? DonePath : FailedPath;
            Directory.CreateDirectory(target);
            var destination = Path.Combine(target, Path.GetFileName(jarPath));
            if (File.Exists(destination)) File.Delete(destination);
            File.Move(jarPath, destination);

            LauncherCommon.Log(result is { Success: true }
                ? $"[ModInbox] 已安装 {Path.GetFileName(jarPath)} -> {result.InstalledPath}"
                : $"[ModInbox] 安装失败 {Path.GetFileName(jarPath)}：{result?.Message}");
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "[ModInbox] 处理 jar 失败");
        }
    }
}
