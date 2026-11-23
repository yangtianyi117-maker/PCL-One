using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace PCL;

public sealed record ModInstallResult(
    string SourcePath,
    string? InstalledPath,
    bool Success,
    string Message,
    ModJarInfo? Info);

/// <summary>
/// 把一批 jar 读成元数据，过滤掉不匹配当前实例的，再复制到实例 mods 目录。
/// 只做“识别 + 安装”，不自动下载依赖；依赖缺失在结果里列出。
/// </summary>
public static class ModJarInstaller
{
    public static async Task<IReadOnlyList<ModInstallResult>> InstallAsync(
        IEnumerable<string> jarPaths,
        string instancePath,
        string minecraftVersion,
        string loader,
        CancellationToken ct = default)
    {
        var results = new List<ModInstallResult>();
        var modsDirectory = Path.Combine(instancePath, "mods");
        Directory.CreateDirectory(modsDirectory);

        foreach (var jarPath in jarPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var info = await ModJarInspector.InspectAsync(jarPath, ct).ConfigureAwait(false);
                if (!LoaderMatches(info.Loader, loader))
                {
                    results.Add(new ModInstallResult(jarPath, null, false,
                        $"加载器不匹配：jar 是 {info.Loader}，实例是 {loader}", info));
                    continue;
                }

                if (!info.IsCompatible(minecraftVersion))
                {
                    results.Add(new ModInstallResult(jarPath, null, false,
                        $"MC 版本不匹配：jar 要求 {info.MinecraftConstraint}，实例是 {minecraftVersion}", info));
                    continue;
                }

                var fileName = Path.GetFileName(jarPath);
                var target = Path.Combine(modsDirectory, fileName);
                if (File.Exists(target) &&
                    string.Equals(await HashAsync(target, ct).ConfigureAwait(false), info.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new ModInstallResult(jarPath, target, true, "已存在相同文件", info));
                    continue;
                }

                File.Copy(jarPath, target, true);
                var missing = info.Dependencies.Count;
                var message = missing == 0
                    ? "已安装"
                    : $"已安装；声明了 {missing} 个依赖（未自动下载）";
                results.Add(new ModInstallResult(jarPath, target, true, message, info));
            }
            catch (Exception ex)
            {
                results.Add(new ModInstallResult(jarPath, null, false, ex.Message, null));
            }
        }

        return results;
    }

    /// <summary>按实例的加载器与 MC 版本判断这个 jar 能否装进去。</summary>
    public static bool Matches(ModJarInfo info, McInstance instance)
    {
        var loader = GetLoader(instance);
        return LoaderMatches(info.Loader, loader) && info.IsCompatible(instance.Info.VanillaName);
    }

    public static string GetLoader(McInstance instance) =>
        instance.Info.HasNeoForge ? "NeoForge"
        : instance.Info.HasForge ? "Forge"
        : instance.Info.HasFabric ? "Fabric"
        : instance.Info.HasQuilt ? "Quilt"
        : "vanilla";

    private static bool LoaderMatches(string jarLoader, string instanceLoader)
    {
        if (string.Equals(jarLoader, instanceLoader, StringComparison.OrdinalIgnoreCase)) return true;
        // Quilt 可以加载 Fabric 模组
        return string.Equals(instanceLoader, "Quilt", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(jarLoader, "Fabric", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> HashAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
