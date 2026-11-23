using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link.Pack;

/// <summary>
/// 通过 SHA-512 反查 Modrinth 版本信息。只解析来源，不下载文件。
/// </summary>
public sealed class ModrinthPackSourceResolver : IPackSourceResolver, IDisposable
{
    private const string ApiBase = "https://api.modrinth.com/v2";
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public ModrinthPackSourceResolver(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("PCLPro", "1.0"));
    }

    public async Task<PackSourceInfo?> ResolveAsync(
        string filePath, string sha256, CancellationToken ct = default)
    {
        var sha512 = await _ComputeSha512Async(filePath, ct).ConfigureAwait(false);
        using var response = await _httpClient
            .GetAsync($"{ApiBase}/version_file/{sha512}?algorithm=sha512", ct)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            return null;

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = document.RootElement;

        var projectId = _GetString(root, "project_id");
        var versionId = _GetString(root, "id");
        if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(versionId))
            return null;

        string? blobUrl = null;
        if (root.TryGetProperty("files", out var files) &&
            files.ValueKind == JsonValueKind.Array &&
            files.GetArrayLength() > 0)
        {
            foreach (var file in files.EnumerateArray())
            {
                if (file.ValueKind != JsonValueKind.Object)
                    continue;
                if (file.TryGetProperty("primary", out var primary) &&
                    primary.ValueKind == JsonValueKind.True)
                {
                    blobUrl = _GetString(file, "url");
                    break;
                }
            }

            if (blobUrl is null && files[0].ValueKind == JsonValueKind.Object)
                blobUrl = _GetString(files[0], "url");
        }

        return new PackSourceInfo
        {
            Source = PackConstants.SourceModrinth,
            ProjectId = projectId,
            VersionId = versionId,
            BlobUrl = blobUrl
        };
    }

    private static string? _GetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static async Task<string> _ComputeSha512Async(string filePath, CancellationToken ct)
    {
        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
        var hash = await SHA512.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }
}

/// <summary>
/// 按顺序尝试多个来源解析器，第一个返回非 null 的结果生效。
/// </summary>
public sealed class CompositePackSourceResolver(params IPackSourceResolver[] resolvers) : IPackSourceResolver
{
    public async Task<PackSourceInfo?> ResolveAsync(
        string filePath, string sha256, CancellationToken ct = default)
    {
        foreach (var resolver in resolvers)
        {
            try
            {
                var result = await resolver.ResolveAsync(filePath, sha256, ct).ConfigureAwait(false);
                if (result is not null)
                    return result;
            }
            catch (HttpRequestException)
            {
                // 公开源不可用时退回下一个解析器（通常是私有源）。
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // 超时同样视为该源不可用。
            }
        }

        return null;
    }
}
