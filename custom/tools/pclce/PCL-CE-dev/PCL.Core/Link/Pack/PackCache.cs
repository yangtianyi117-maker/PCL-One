using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link.Pack;

/// <summary>
/// 以 SHA-256 为键的内容缓存。相同 mod 在多个受管实例之间复用。
/// </summary>
public sealed class PackCache(string root, long limitBytes = 2L * 1024 * 1024 * 1024)
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    public string Root { get; } = root;

    public async Task<string> GetOrAddAsync(
        string sha256,
        string fileName,
        Func<CancellationToken, Task<Stream>> fetch,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sha256))
            throw new ArgumentException("sha256 不能为空", nameof(sha256));

        var key = sha256.ToLowerInvariant();
        var target = Path.Combine(Root, key);
        if (File.Exists(target) && await HashMatchesAsync(target, key, ct).ConfigureAwait(false))
            return target;

        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (File.Exists(target) && await HashMatchesAsync(target, key, ct).ConfigureAwait(false))
                return target;

            Directory.CreateDirectory(Root);
            var temp = target + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                await using (var source = await fetch(ct).ConfigureAwait(false))
                await using (var output = new FileStream(
                                 temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024,
                                 FileOptions.Asynchronous))
                {
                    await source.CopyToAsync(output, ct).ConfigureAwait(false);
                }

                if (!await HashMatchesAsync(temp, key, ct).ConfigureAwait(false))
                    throw new InvalidOperationException($"缓存文件哈希不匹配：{fileName}");

                File.Move(temp, target, true);
                return target;
            }
            finally
            {
                if (File.Exists(temp))
                {
                    try { File.Delete(temp); }
                    catch (Exception) { /* best effort */ }
                }
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public static void LinkOrCopy(string sourcePath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        if (File.Exists(targetPath)) File.Delete(targetPath);

        if (OperatingSystem.IsWindows() &&
            CreateHardLink(targetPath, sourcePath, IntPtr.Zero))
            return;

        File.Copy(sourcePath, targetPath, true);
    }

    public async Task PruneAsync(long? maxBytes = null, CancellationToken ct = default)
    {
        if (!Directory.Exists(Root)) return;

        var limit = maxBytes ?? limitBytes;
        var files = new DirectoryInfo(Root)
            .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
            .Where(file => !file.Name.Contains(".tmp-", StringComparison.Ordinal))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();

        long total = 0;
        foreach (var file in files)
        {
            total += file.Length;
            if (total <= limit) continue;

            ct.ThrowIfCancellationRequested();
            try { file.Delete(); }
            catch (Exception) { /* best effort */ }
            await Task.Yield();
        }
    }

    private static async Task<bool> HashMatchesAsync(string path, string expected, CancellationToken ct)
    {
        try
        {
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
            var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
            return string.Equals(
                Convert.ToHexString(hash), expected, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLink(
        string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);
}
