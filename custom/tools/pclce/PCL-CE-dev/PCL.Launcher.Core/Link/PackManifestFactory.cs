using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Link.Pack;

namespace PCL;

/// <summary>
/// 把 PCL 的实例信息转换为内容清单构建参数。
/// </summary>
public static class PackManifestFactory
{
    public static (string Loader, string LoaderVersion) GetLoader(McInstanceInfo info)
    {
        if (info.HasNeoForge)
            return ("neoforge", info.NeoForge ?? string.Empty);
        if (info.HasForge)
            return ("forge", info.Forge ?? string.Empty);
        if (info.HasFabric)
            return ("fabric", info.Fabric ?? string.Empty);
        if (info.HasQuilt)
            return ("quilt", info.Quilt ?? string.Empty);
        if (info.HasLegacyFabric)
            return ("legacyfabric", info.LegacyFabric ?? string.Empty);

        return ("vanilla", string.Empty);
    }

    public static PackBuildOptions CreateOptions(
        McInstance instance,
        string? hostName = null,
        string? hostPublicKey = null)
    {
        var (loader, loaderVersion) = GetLoader(instance.Info);
        return new PackBuildOptions
        {
            InstancePath = instance.PathInstance,
            MinecraftVersion = instance.Info.VanillaName,
            Loader = loader,
            LoaderVersion = loaderVersion,
            HostName = hostName,
            HostPublicKey = hostPublicKey
        };
    }

    public static Task<PackManifest> BuildAsync(
        McInstance instance,
        IPackSourceResolver sourceResolver,
        string? hostName = null,
        string? hostPublicKey = null,
        CancellationToken ct = default)
    {
        var builder = new PackManifestBuilder(sourceResolver);
        return builder.BuildAsync(
            CreateOptions(instance, hostName, hostPublicKey), ct);
    }
}
