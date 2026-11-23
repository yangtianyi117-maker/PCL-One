using System;
using System.Buffers;
using System.Text;
using System.Text.Json;
using PCL.Core.Link.Pack;
using PCL.Core.Link.Scaffolding.Client.Abstractions;

namespace PCL.Core.Link.Scaffolding.Client.Requests;

/// <summary>
/// 向房主请求内容清单。请求体可选地携带客户端当前的 packHash，便于房主返回 304 或增量。
/// </summary>
public sealed class GetPackManifestRequest(string? currentPackHash = null) : IRequest<PackManifest?>
{
    /// <inheritdoc />
    public string RequestType { get; } = PackConstants.ManifestRequestType;

    /// <inheritdoc />
    public void WriteRequestBody(IBufferWriter<byte> writer)
    {
        if (string.IsNullOrWhiteSpace(currentPackHash))
            return;

        var bytes = Encoding.UTF8.GetBytes(currentPackHash);
        writer.Write(bytes);
    }

    /// <inheritdoc />
    public PackManifest? ParseResponseBody(ReadOnlyMemory<byte> responseBody)
    {
        if (responseBody.IsEmpty)
            return null;

        return JsonSerializer.Deserialize<PackManifest>(responseBody.Span, PackJson.Options);
    }
}
