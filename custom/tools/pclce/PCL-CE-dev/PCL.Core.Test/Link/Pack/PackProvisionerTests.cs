using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link.Pack;

namespace PCL.Core.Test.Link.Pack;

[TestClass]
public sealed class PackProvisionerTests
{
    [TestMethod]
    public async Task Ensure_WritesModsConfigsAndMarker_ThenReuses()
    {
        var root = CreateTempDirectory();
        try
        {
            var modBytes = Encoding.UTF8.GetBytes("mod-bytes");
            var configBytes = Encoding.UTF8.GetBytes("config-bytes");

            var manifest = CreateManifest(
                Hash(modBytes),
                Hash(configBytes));

            var versionsRoot = Path.Combine(root, "versions");
            Directory.CreateDirectory(versionsRoot);

            var installer = new FakeInstaller();
            var fetcher = new FakeFetcher(new Dictionary<string, byte[]>
            {
                ["mem://mod"] = modBytes,
                ["mem://config"] = configBytes
            });
            var provisioner = new PackProvisioner(
                installer, fetcher, new BlobOnlyPackModUrlResolver(), versionsRoot);

            var first = await provisioner.EnsureAsync(manifest);
            Assert.IsTrue(first.Success, first.Error);
            Assert.AreEqual(1, installer.Calls);
            Assert.IsTrue(File.Exists(Path.Combine(first.InstancePath, "mods", "foo.jar")));
            Assert.IsTrue(File.Exists(Path.Combine(first.InstancePath, "config", "foo.toml")));
            Assert.IsTrue(File.Exists(Path.Combine(first.InstancePath, "pclpro.json")));

            var second = await provisioner.EnsureAsync(manifest);
            Assert.IsTrue(second.Success, second.Error);
            Assert.AreEqual(1, installer.Calls, "同 packHash 应复用实例");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [TestMethod]
    public async Task Ensure_FailsOnHashMismatch()
    {
        var root = CreateTempDirectory();
        try
        {
            var manifest = CreateManifest(Hash(Encoding.UTF8.GetBytes("expected")), null);
            var versionsRoot = Path.Combine(root, "versions");
            Directory.CreateDirectory(versionsRoot);

            var provisioner = new PackProvisioner(
                new FakeInstaller(),
                new FakeFetcher(new Dictionary<string, byte[]>
                {
                    ["mem://mod"] = Encoding.UTF8.GetBytes("other")
                }),
                new BlobOnlyPackModUrlResolver(),
                versionsRoot);

            var result = await provisioner.EnsureAsync(manifest);
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error?.Contains("哈希") == true, result.Error);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [TestMethod]
    public async Task Ensure_VanillaWithoutMods_StillInstallsInstance()
    {
        var root = CreateTempDirectory();
        try
        {
            var manifest = new PackManifest
            {
                MinecraftVersion = "1.21.1",
                Loader = "vanilla",
                LoaderVersion = "",
                Mods = [],
                Configs = []
            };
            manifest = manifest with { PackHash = PackManifestHasher.ComputeHash(manifest) };

            var versionsRoot = Path.Combine(root, "versions");
            Directory.CreateDirectory(versionsRoot);
            var installer = new FakeInstaller();
            var provisioner = new PackProvisioner(
                installer,
                new FakeFetcher(new Dictionary<string, byte[]>()),
                new BlobOnlyPackModUrlResolver(),
                versionsRoot);

            var result = await provisioner.EnsureAsync(manifest);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, installer.Calls);
            Assert.IsTrue(File.Exists(Path.Combine(result.InstancePath, "pclpro.json")));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static PackManifest CreateManifest(string modSha256, string? configSha256)
    {
        var manifest = new PackManifest
        {
            MinecraftVersion = "1.21.1",
            Loader = "neoforge",
            LoaderVersion = "21.1.80",
            Mods =
            [
                new PackMod
                {
                    Source = PackConstants.SourcePrivate,
                    Sha256 = modSha256,
                    FileName = "foo.jar",
                    BlobUrl = "mem://mod"
                }
            ],
            Configs = configSha256 is null
                ? []
                :
                [
                    new PackConfig
                    {
                        Path = "config/foo.toml",
                        Sha256 = configSha256,
                        BlobUrl = "mem://config"
                    }
                ]
        };

        return manifest with { PackHash = PackManifestHasher.ComputeHash(manifest) };
    }

    private static string Hash(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "PCLProTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (Exception)
        {
            // best effort
        }
    }

    private sealed class FakeInstaller : IPackInstanceInstaller
    {
        public int Calls { get; private set; }

        public Task<bool> EnsureInstanceAsync(PackInstallRequest request, CancellationToken ct = default)
        {
            Calls++;
            Directory.CreateDirectory(request.InstancePath);
            File.WriteAllText(
                Path.Combine(request.InstancePath, request.InstanceName + ".json"),
                "{}");
            return Task.FromResult(true);
        }
    }

    private sealed class FakeFetcher(IReadOnlyDictionary<string, byte[]> files) : IPackFileFetcher
    {
        public Task<Stream> OpenReadAsync(string url, CancellationToken ct = default)
        {
            if (!files.TryGetValue(url, out var bytes))
                throw new FileNotFoundException(url);

            return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        }
    }
}
