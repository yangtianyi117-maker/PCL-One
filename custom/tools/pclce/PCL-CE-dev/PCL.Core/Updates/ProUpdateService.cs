using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.Logging;

namespace PCL.Core.Updates;

/// <summary>签名清单里的一条更新信息。</summary>
public sealed record ProUpdateInfo(
    string Build,
    int BuildNumber,
    string Sha256,
    long Size,
    string PayloadUrl,
    string PublishedAt);

/// <summary>
/// PCL 定制版 的签名自动更新：
///
/// 第一性：更新 = 固定通道 + 内置公钥 + 下载验签 + 原子替换。
/// 公钥写死在这里，绝不从下载内容里读取；manifest 签名过了才看 exe 的 SHA-256。
/// 只替换 bootstrapper（PCLPro.exe），运行中的 PCLProRuntime 不动；
/// 下次启动由 bootstrapper 按 .payload-version 解压新运行时，PCL/ 数据保留。
/// </summary>
public static class ProUpdateService
{
    // secrets/pclpro-update-key.json 对应的 P-256 公钥；换钥必须重新发版。
    public const string PublicKeyX = "/4l3mL25OSd5tobHYzZBCUOI3sMSaPmkiI/zGD22ZpE=";
    public const string PublicKeyY = "gUOBQmts3d8HRVBF7SVrhMvzFUNUcR6+G1BfDWh6/AA=";

    private static readonly HttpClient ManifestClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static readonly HttpClient DownloadClient = new()
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    public static async Task<ProUpdateInfo?> CheckAsync(string baseUrl, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
            return null;
        if (baseUri.Scheme is not ("http" or "https"))
            return null;

        try
        {
            var manifestBytes = await ManifestClient
                .GetByteArrayAsync(new Uri(baseUri, "update.json"), ct)
                .ConfigureAwait(false);
            var signatureText = await ManifestClient
                .GetStringAsync(new Uri(baseUri, "update.json.sig"), ct)
                .ConfigureAwait(false);

            var signature = Convert.FromBase64String(signatureText.Trim());
            if (!VerifySignature(manifestBytes, signature))
            {
                LogWrapper.Warn("ProUpdate", "更新清单签名无效，已拒绝");
                return null;
            }

            using var document = JsonDocument.Parse(manifestBytes);
            var root = document.RootElement;

            var buildNumber = root.TryGetProperty("buildNumber", out var buildNumberElement)
                ? buildNumberElement.GetInt32()
                : 0;
            if (buildNumber <= ProEdition.BuildNumber)
                return null;

            var build = root.TryGetProperty("build", out var buildElement)
                ? buildElement.GetString() ?? ""
                : "";
            var sha256 = root.TryGetProperty("sha256", out var shaElement)
                ? shaElement.GetString()?.ToLowerInvariant() ?? ""
                : "";
            var size = root.TryGetProperty("size", out var sizeElement)
                ? sizeElement.GetInt64()
                : 0;
            var publishedAt = root.TryGetProperty("publishedAt", out var publishedElement)
                ? publishedElement.GetString() ?? ""
                : "";

            var payload = root.TryGetProperty("payloadUrl", out var payloadElement)
                ? payloadElement.GetString()
                : null;
            var payloadUrl = string.IsNullOrWhiteSpace(payload)
                ? new Uri(baseUri, "PCLPro.exe").ToString()
                : payload;

            if (sha256.Length != 64 || size <= 0 ||
                !Uri.TryCreate(payloadUrl, UriKind.Absolute, out var payloadUri) ||
                payloadUri.Scheme is not ("http" or "https"))
            {
                LogWrapper.Warn("ProUpdate", "更新清单字段无效，已拒绝");
                return null;
            }

            return new ProUpdateInfo(build, buildNumber, sha256, size, payloadUrl, publishedAt);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("ProUpdate", $"检查更新失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>下载并验签 exe，成功后原子替换 bootstrapper。</summary>
    public static async Task<bool> DownloadAndStageAsync(
        ProUpdateInfo info,
        string targetExe,
        Action<string>? log = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(targetExe) ||
            !string.Equals(Path.GetFileName(targetExe), "PCLPro.exe", StringComparison.OrdinalIgnoreCase))
            return false;

        var temp = targetExe + ".new";
        var maxAttempts = 3;
        log?.Invoke($"[ProUpdate] 下载 {info.Build}：{info.PayloadUrl}");

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var offset = File.Exists(temp) ? new FileInfo(temp).Length : 0;
                if (offset > info.Size)
                {
                    File.Delete(temp);
                    offset = 0;
                }

                if (offset == info.Size)
                {
                    log?.Invoke("[ProUpdate] 已有完整临时文件，直接校验");
                }
                else
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, info.PayloadUrl);
                    request.Headers.UserAgent.ParseAdd("PCLPro/1.0");
                    if (offset > 0)
                        request.Headers.Range = new RangeHeaderValue(offset, null);

                    using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    attemptCts.CancelAfter(TimeSpan.FromMinutes(3));

                    using var response = await DownloadClient
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, attemptCts.Token)
                        .ConfigureAwait(false);

                    if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                    {
                        log?.Invoke("[ProUpdate] 断点续传被拒绝，重新下载");
                        File.Delete(temp);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();

                    var isResume = offset > 0 && response.StatusCode == HttpStatusCode.PartialContent;
                    if (!isResume) offset = 0;

                    if (response.Content.Headers.ContentLength is { } contentLength &&
                        contentLength != info.Size - offset)
                    {
                        log?.Invoke($"[ProUpdate] 分块大小不符：{contentLength} != {info.Size - offset}");
                        File.Delete(temp);
                        continue;
                    }

                    await using (var source = await response.Content
                                     .ReadAsStreamAsync(attemptCts.Token).ConfigureAwait(false))
                    await using (var target = new FileStream(
                                     temp,
                                     isResume ? FileMode.Append : FileMode.Create,
                                     FileAccess.Write,
                                     FileShare.None,
                                     64 * 1024,
                                     useAsync: true))
                    {
                        await source.CopyToAsync(target, 64 * 1024, attemptCts.Token).ConfigureAwait(false);
                    }
                }

                if (new FileInfo(temp).Length != info.Size)
                {
                    log?.Invoke($"[ProUpdate] 下载未完成（第 {attempt} 次），继续续传");
                    continue;
                }

                string hash;
                await using (var stream = File.OpenRead(temp))
                {
                    hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false))
                        .ToLowerInvariant();
                }

                if (!string.Equals(hash, info.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    log?.Invoke($"[ProUpdate] SHA-256 不符：{hash}，重新下载");
                    File.Delete(temp);
                    continue;
                }

                File.Copy(targetExe, targetExe + ".old", true);
                File.Move(temp, targetExe, true);
                log?.Invoke($"[ProUpdate] 已替换 bootstrapper：{targetExe}");
                return true;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                log?.Invoke($"[ProUpdate] 第 {attempt} 次下载超时，准备续传");
            }
            catch (Exception ex)
            {
                log?.Invoke($"[ProUpdate] 第 {attempt} 次下载失败：{ex.Message}");
            }

            if (attempt < maxAttempts)
            {
                try { await Task.Delay(1500, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return false; }
            }
        }

        log?.Invoke("[ProUpdate] 下载最终失败，保留临时文件，下次启动可续传");
        return false;
    }

    private static bool VerifySignature(byte[] data, byte[] signature)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportParameters(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = Convert.FromBase64String(PublicKeyX),
                Y = Convert.FromBase64String(PublicKeyY)
            }
        });
        return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
    }
}
