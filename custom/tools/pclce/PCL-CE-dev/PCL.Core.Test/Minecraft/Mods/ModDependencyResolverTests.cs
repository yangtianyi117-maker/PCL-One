using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL;

namespace PCL.Core.Test.Minecraft.Mods;

[TestClass]
public sealed class ModDependencyResolverTests
{
    [TestMethod]
    public async Task Resolve_ReadsFirstMatchingModrinthVersion()
    {
        var payload = Encoding.UTF8.GetBytes("fake-jar");
        var sha512 = Convert.ToHexString(SHA512.HashData(payload)).ToLowerInvariant();
        using var handler = new FakeHandler(new Dictionary<string, HttpResponseMessage>
        {
            ["/v2/project/fabric-api/version"] = Json(
                "[{\"project_id\":\"P7dR8mSH\",\"id\":\"ver1\",\"files\":[{\"filename\":\"fabric-api.jar\",\"url\":\"https://cdn.example/fabric-api.jar\",\"primary\":true,\"hashes\":{\"sha512\":\"" + sha512 + "\"}}]}]")
        });
        using var resolver = new ModrinthDependencyResolver(new HttpClient(handler));

        var result = await resolver.ResolveAsync("fabric-api", "1.21.1", "Fabric");

        Assert.IsNotNull(result);
        Assert.AreEqual("fabric-api.jar", result!.FileName);
        Assert.AreEqual(sha512, result.Sha512);
    }

    [TestMethod]
    public async Task InstallDependencies_DownloadsAndVerifies()
    {
        var payload = Encoding.UTF8.GetBytes("fake-jar-content");
        var sha512 = Convert.ToHexString(SHA512.HashData(payload)).ToLowerInvariant();
        using var handler = new FakeHandler(new Dictionary<string, HttpResponseMessage>
        {
            ["/v2/project/fabric-api/version"] = Json(
                "[{\"project_id\":\"P7dR8mSH\",\"id\":\"ver1\",\"files\":[{\"filename\":\"fabric-api.jar\",\"url\":\"https://cdn.example/fabric-api.jar\",\"primary\":true,\"hashes\":{\"sha512\":\"" + sha512 + "\"}}]}]"),
            ["/fabric-api.jar"] = Bytes(payload)
        });
        using var resolver = new ModrinthDependencyResolver(new HttpClient(handler));
        var root = CreateTempDirectory();
        try
        {
            var info = new ModJarInfo
            {
                Path = "root.jar",
                Loader = "Fabric",
                Dependencies = [new ModDependency("fabric-api", null, false, "Fabric")]
            };

            Assert.AreEqual(1, info.Dependencies.Count, "test setup");
            var results = await resolver.InstallDependenciesAsync(info, root, "1.21.1", "Fabric");

            Assert.AreEqual(1, results.Count, string.Join(" | ", results.Select(r => r.Message)));
            Assert.IsTrue(results[0].Success, results[0].Message);
            Assert.IsTrue(File.Exists(Path.Combine(root, "mods", "fabric-api.jar")));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage Bytes(byte[] content) => new(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(content)
    };

    private sealed class FakeHandler(Dictionary<string, HttpResponseMessage> routes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(routes.TryGetValue(path, out var response)
                ? response
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pcl-dep-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        try { Directory.Delete(path, true); }
        catch (Exception) { /* best effort */ }
    }
}
