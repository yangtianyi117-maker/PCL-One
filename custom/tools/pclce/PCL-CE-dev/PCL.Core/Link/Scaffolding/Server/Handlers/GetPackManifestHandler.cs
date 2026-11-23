using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Link.Pack;
using PCL.Core.Link.Scaffolding.Server.Abstractions;

namespace PCL.Core.Link.Scaffolding.Server.Handlers;

/// <summary>
/// 返回房主发布的内容清单。房主未配置清单时返回 32。
/// </summary>
public sealed class GetPackManifestHandler : IRequestHandler
{
    private const int MaxManifestBytes = 60_000;

    /// <inheritdoc />
    public string RequestType { get; } = PackConstants.ManifestRequestType;

    /// <inheritdoc />
    public Task<(byte Status, ReadOnlyMemory<byte> Body)> HandleAsync(
        ReadOnlyMemory<byte> requestBody, IServerContext context, string sessionId, CancellationToken ct)
    {
        var manifest = context.PackManifest;
        if (manifest is null)
            return Task.FromResult(((byte)32, ReadOnlyMemory<byte>.Empty));

        var body = JsonSerializer.SerializeToUtf8Bytes(manifest, PackJson.Options);
        if (body.Length > MaxManifestBytes)
            return Task.FromResult(((byte)33, ReadOnlyMemory<byte>.Empty));

        return Task.FromResult(((byte)0, new ReadOnlyMemory<byte>(body)));
    }
}
