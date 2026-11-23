using System;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link.Pack;

/// <summary>
/// 一个 mod 文件对应的来源信息。公开源只需要 project/version；私有源需要 blob。
/// </summary>
public sealed record PackSourceInfo
{
    public string Source { get; init; } = PackConstants.SourcePrivate;
    public string? ProjectId { get; init; }
    public string? VersionId { get; init; }
    public string? BlobUrl { get; init; }
    public string Side { get; init; } = PackConstants.SideBoth;
}

/// <summary>
/// 把本地 mod 文件解析成可分发来源。实现可以查 Modrinth/CurseForge，也可以直接标私有。
/// </summary>
public interface IPackSourceResolver
{
    Task<PackSourceInfo?> ResolveAsync(string filePath, string sha256, CancellationToken ct = default);
}

/// <summary>
/// 默认解析器：不联网，所有文件都标为私有，由房主决定是否提供 blob。
/// </summary>
public sealed class PrivatePackSourceResolver : IPackSourceResolver
{
    public Task<PackSourceInfo?> ResolveAsync(string filePath, string sha256, CancellationToken ct = default)
        => Task.FromResult<PackSourceInfo?>(new PackSourceInfo
        {
            Source = PackConstants.SourcePrivate
        });
}

/// <summary>
/// 测试或宿主注入用的委托解析器。
/// </summary>
public sealed class DelegatePackSourceResolver(
    Func<string, string, CancellationToken, Task<PackSourceInfo?>> resolver) : IPackSourceResolver
{
    public Task<PackSourceInfo?> ResolveAsync(string filePath, string sha256, CancellationToken ct = default)
        => resolver(filePath, sha256, ct);
}
