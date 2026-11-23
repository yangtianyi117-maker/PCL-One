using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link.Pack;

namespace PCL.Core.Test.Link.Pack;

[TestClass]
public sealed class PackCacheTests
{
    [TestMethod]
    public async Task GetOrAdd_DownloadsOnceAndReuses()
    {
        var root = CreateTempDirectory();
        try
        {
            var bytes = Encoding.UTF8.GetBytes("cached-bytes");
            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var cache = new PackCache(Path.Combine(root, "cache"));
            var calls = 0;

            Task<Stream> Fetch(CancellationToken _)
            {
                calls++;
                return Task.FromResult<Stream>(new MemoryStream(bytes, false));
            }

            var first = await cache.GetOrAddAsync(sha, "a.jar", Fetch);
            var second = await cache.GetOrAddAsync(sha, "a.jar", Fetch);

            Assert.AreEqual(first, second);
            Assert.AreEqual(1, calls);
            CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(second));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [TestMethod]
    public async Task GetOrAdd_RejectsHashMismatch()
    {
        var root = CreateTempDirectory();
        try
        {
            var cache = new PackCache(Path.Combine(root, "cache"));
            var sha = new string('0', 64);

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                cache.GetOrAddAsync(sha, "a.jar", _ =>
                    Task.FromResult<Stream>(new MemoryStream([1, 2, 3], false))));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pcl-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        try { Directory.Delete(path, true); }
        catch (Exception) { /* best effort */ }
    }
}
