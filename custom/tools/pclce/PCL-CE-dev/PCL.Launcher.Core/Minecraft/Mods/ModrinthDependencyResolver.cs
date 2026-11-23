using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PCL;

public sealed record ResolvedModFile(
    string ModId,
    string ProjectId,
    string VersionId,
    string FileName,
    string Url,
    string? Sha512,
    string? Sha1);

/// <summary>
/// 按 jar 里的依赖声明去 Modrinth 找对应版本并下载。
/// 只处理 required 依赖；可选依赖跳过。找不到时返回错误，不阻塞已安装的模组。
/// </summary>
public sealed class ModrinthDependencyResolver : IDisposable
{
    private const string ApiBase = "https://api.modrinth.com/v2";
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public ModrinthDependencyResolver(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PCLPro", "1.0"));
    }

    public async Task<IReadOnlyList<ModInstallResult>> InstallDependenciesAsync(
        ModJarInfo info,
        string instancePath,
        string minecraftVersion,
        string loader,
        int maxDepth = 2,
        CancellationToken ct = default)
    {
        var results = new List<ModInstallResult>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await InstallRecursiveAsync(info, instancePath, minecraftVersion, loader, maxDepth, seen, results, ct)
            .ConfigureAwait(false);
        return results;
    }

    private async Task InstallRecursiveAsync(
        ModJarInfo info,
        string instancePath,
        string minecraftVersion,
        string loader,
        int depth,
        HashSet<string> seen,
        List<ModInstallResult> results,
        CancellationToken ct)
    {
        if (depth < 0) return;
        foreach (var dependency in info.Dependencies)
        {
            ct.ThrowIfCancellationRequested();
            if (dependency.Optional) continue;
            if (DependencyIds.IsPlatform(dependency.ModId)) continue;
            if (!seen.Add(dependency.ModId)) continue;

            try
            {
                var resolved = await ResolveAsync(dependency.ModId, minecraftVersion, loader, ct)
                    .ConfigureAwait(false);
                if (resolved is null)
                {
                    results.Add(new ModInstallResult(info.Path, null, false,
                        $"依赖 {dependency.ModId} 在 Modrinth 没有找到匹配版本", info));
                    continue;
                }

                var mods = Path.Combine(instancePath, "mods");
                Directory.CreateDirectory(mods);
                var target = Path.Combine(mods, resolved.FileName);
                if (File.Exists(target) && await MatchesAsync(target, resolved, ct).ConfigureAwait(false))
                {
                    results.Add(new ModInstallResult(resolved.Url, target, true, "依赖已存在", null));
                }
                else
                {
                    var temp = Path.Combine(Path.GetTempPath(),
                        "pcl-dep-" + Guid.NewGuid().ToString("N") + ".jar");
                    await DownloadAsync(resolved, temp, ct).ConfigureAwait(false);
                    File.Copy(temp, target, true);
                    File.Delete(temp);
                    results.Add(new ModInstallResult(resolved.Url, target, true,
                        $"已安装依赖 {resolved.ModId}", null));
                }

                // 依赖自己的依赖：只读已下载 jar，避免递归查询无限展开
                if (depth > 0 && File.Exists(target))
                {
                    try
                    {
                        var nested = await ModJarInspector.InspectAsync(target, ct).ConfigureAwait(false);
                        await InstallRecursiveAsync(nested, instancePath, minecraftVersion, loader,
                            depth - 1, seen, results, ct).ConfigureAwait(false);
                    }
                    catch (InvalidDataException)
                    {
                        // 不是合法 jar（或测试桩），依赖本身已安装，跳过递归即可
                    }
                }
            }
            catch (Exception ex)
            {
                results.Add(new ModInstallResult(info.Path, null, false,
                    $"依赖 {dependency.ModId} 安装失败：{ex.Message}", info));
            }
        }
    }

    public async Task<ResolvedModFile?> ResolveAsync(
        string modId,
        string minecraftVersion,
        string loader,
        CancellationToken ct = default)
    {
        var loaderName = loader.ToLowerInvariant() switch
        {
            "quilt" => "quilt",
            "neoforge" => "neoforge",
            "forge" => "forge",
            _ => "fabric"
        };
        var game = Uri.EscapeDataString($"[\"{minecraftVersion}\"]");
        var loaders = Uri.EscapeDataString($"[\"{loaderName}\"]");
        var url = $"{ApiBase}/project/{Uri.EscapeDataString(modId)}/version?game_versions={game}&loaders={loaders}";
        using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            return null;
        response.EnsureSuccessStatusCode();
        return await ParseFirstVersionAsync(modId, response, ct).ConfigureAwait(false);
    }

    private static async Task<ResolvedModFile?> ParseFirstVersionAsync(
        string modId, HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0) return null;
        var version = root[0];
        var projectId = version.TryGetProperty("project_id", out var p) ? p.GetString() ?? modId : modId;
        var versionId = version.TryGetProperty("id", out var v) ? v.GetString() ?? "" : "";
        if (!version.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
            return null;

        JsonElement? chosen = null;
        foreach (var file in files.EnumerateArray())
        {
            if (file.TryGetProperty("primary", out var primary) && primary.ValueKind == JsonValueKind.True)
            {
                chosen = file;
                break;
            }
        }

        chosen ??= files.GetArrayLength() > 0 ? files[0] : null;
        if (chosen is not { } selected) return null;
        var fileName = selected.TryGetProperty("filename", out var fn) ? fn.GetString() ?? "" : "";
        var url = selected.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(url)) return null;
        string? sha512 = null, sha1 = null;
        if (selected.TryGetProperty("hashes", out var hashes) && hashes.ValueKind == JsonValueKind.Object)
        {
            sha512 = hashes.TryGetProperty("sha512", out var s5) ? s5.GetString() : null;
            sha1 = hashes.TryGetProperty("sha1", out var s1) ? s1.GetString() : null;
        }

        return new ResolvedModFile(modId, projectId, versionId, fileName, url, sha512, sha1);
    }

    private async Task DownloadAsync(ResolvedModFile resolved, string path, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(resolved.Url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using (var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
                         64 * 1024, FileOptions.Asynchronous))
        {
            await input.CopyToAsync(output, ct).ConfigureAwait(false);
        }

        await using var check = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (!string.IsNullOrWhiteSpace(resolved.Sha512))
        {
            var actual = Convert.ToHexString(await SHA512.HashDataAsync(check, ct).ConfigureAwait(false))
                .ToLowerInvariant();
            if (!string.Equals(actual, resolved.Sha512, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("依赖 SHA-512 校验失败");
        }
    }

    private static async Task<bool> MatchesAsync(string path, ResolvedModFile resolved, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(resolved.Sha512)) return true;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var actual = Convert.ToHexString(await SHA512.HashDataAsync(stream, ct).ConfigureAwait(false))
            .ToLowerInvariant();
        return string.Equals(actual, resolved.Sha512, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }
}
