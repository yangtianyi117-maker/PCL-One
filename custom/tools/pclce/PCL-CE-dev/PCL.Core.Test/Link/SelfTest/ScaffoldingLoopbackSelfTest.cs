using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link.Pack;
using PCL.Core.Link.Scaffolding.Client;
using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Link.Scaffolding.Client.Requests;
using PCL.Core.Link.Scaffolding.Server;
using PCL.Core.Link.Scaffolding.Server.Abstractions;

namespace PCL.Core.Test.Link.SelfTest;

/// <summary>
/// 真实 TCP 回环联机自测：房主 ScaffoldingServer + 加入者 ScaffoldingClient，
/// 走完整握手、c:pack_manifest 请求、签名校验和受管实例物化。
/// </summary>
[TestClass]
public sealed class ScaffoldingLoopbackSelfTest
{
    [TestMethod]
    public async Task HostAndJoiner_ExchangeSignedManifest_AndProvisionInstance()
    {
        var root = CreateTempDirectory();
        try
        {
            var modBytes = Encoding.UTF8.GetBytes("pclpro-selftest-mod");
            var modHash = Convert.ToHexString(SHA256.HashData(modBytes)).ToLowerInvariant();

            var manifest = await BuildSignedManifestAsync(root, modBytes, modHash);
            var port = GetFreePort();

            var context = new FakeServerContext(manifest);
            await using var server = new ScaffoldingServer(port, context);
            server.Start();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await using var client = new ScaffoldingClient(
                "127.0.0.1", port, "joiner", "machine-joiner", "PCLPro selftest");
            await client.ConnectAsync(cts.Token);

            Assert.IsTrue(client.IsConnected);

            var received = await client.SendRequestAsync(new GetPackManifestRequest(), cts.Token);
            Assert.IsNotNull(received);
            Assert.AreEqual(manifest.PackHash, received.PackHash);
            Assert.IsTrue(PackManifestSigner.VerifySignature(received));

            var versionsRoot = Path.Combine(root, "versions");
            Directory.CreateDirectory(versionsRoot);
            var provisioner = new PackProvisioner(
                new FakeInstaller(),
                new FakeFetcher(new Dictionary<string, byte[]> { ["mem://mod"] = modBytes }),
                new BlobOnlyPackModUrlResolver(),
                versionsRoot);

            var provisioned = await provisioner.EnsureAsync(received, cts.Token);
            Assert.IsTrue(provisioned.Success, provisioned.Error);
            Assert.IsTrue(File.Exists(Path.Combine(provisioned.InstancePath, "mods", "selftest.jar")));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [TestMethod]
    public async Task HostAndJoiner_VanillaZeroMod_StillExchangesManifest()
    {
        var root = CreateTempDirectory();
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var manifest = PackManifestSigner.Sign(new PackManifest
            {
                MinecraftVersion = "1.21.1",
                Loader = "vanilla",
                LoaderVersion = "",
                Mods = [],
                Configs = [],
                Host = new PackHost { Name = "host" }
            }, key);

            var port = GetFreePort();
            var context = new FakeServerContext(manifest);
            await using var server = new ScaffoldingServer(port, context);
            server.Start();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await using var client = new ScaffoldingClient(
                "127.0.0.1", port, "joiner", "machine-joiner", "PCLPro selftest");
            await client.ConnectAsync(cts.Token);

            var received = await client.SendRequestAsync(new GetPackManifestRequest(), cts.Token);
            Assert.IsNotNull(received);
            Assert.AreEqual("1.21.1", received.MinecraftVersion);
            Assert.AreEqual("vanilla", received.Loader);
            Assert.AreEqual(0, received.Mods.Count);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static async Task<PackManifest> BuildSignedManifestAsync(
        string root, byte[] modBytes, string modHash)
    {
        Directory.CreateDirectory(Path.Combine(root, "mods"));
        await File.WriteAllBytesAsync(Path.Combine(root, "mods", "selftest.jar"), modBytes);

        var builder = new PackManifestBuilder(new DelegatePackSourceResolver((_, _, _) =>
            Task.FromResult<PackSourceInfo?>(new PackSourceInfo
            {
                Source = PackConstants.SourcePrivate,
                BlobUrl = "mem://mod"
            })));

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var built = await builder.BuildAsync(new PackBuildOptions
        {
            InstancePath = root,
            MinecraftVersion = "1.21.1",
            Loader = "neoforge",
            LoaderVersion = "21.1.0",
            HostName = "host",
            HostPublicKey = PackManifestSigner.PublicKeyBase64(key)
        });

        return PackManifestSigner.Sign(built, key);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pcl-link-selftest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch (Exception)
        {
            // best effort
        }
    }

    private sealed class FakeServerContext(PackManifest manifest) : IServerContext
    {
        public IReadOnlyList<PlayerProfile> PlayerProfiles => [];
        public ConcurrentDictionary<string, TrackedPlayerProfile> TrackedPlayers { get; } = new();
        public void OnPlayerProfilesChanged() { }

        public event Action<IReadOnlyList<PlayerProfile>>? PlayerProfilesPing
        {
            add { }
            remove { }
        }

        public int MinecraftServerProt => 25565;
        public LobbyInfo UserLobbyInfo { get; } = new("code", "network", "secret");
        public string PlayerName => "host";
        public PackManifest PackManifest => manifest;
    }

    private sealed class FakeInstaller : IPackInstanceInstaller
    {
        public Task<bool> EnsureInstanceAsync(PackInstallRequest request, CancellationToken ct = default)
        {
            Directory.CreateDirectory(request.InstancePath);
            File.WriteAllText(
                Path.Combine(request.InstancePath, request.InstanceName + ".json"), "{}");
            return Task.FromResult(true);
        }
    }

    private sealed class FakeFetcher(IReadOnlyDictionary<string, byte[]> files) : IPackFileFetcher
    {
        public Task<Stream> OpenReadAsync(string url, CancellationToken ct = default)
        {
            if (!files.TryGetValue(url, out var bytes))
                throw new FileNotFoundException(url);
            return Task.FromResult<Stream>(new MemoryStream(bytes, false));
        }
    }
}
