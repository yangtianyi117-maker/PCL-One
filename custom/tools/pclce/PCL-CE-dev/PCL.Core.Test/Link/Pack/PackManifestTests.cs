using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link.Pack;

namespace PCL.Core.Test.Link.Pack;

[TestClass]
public sealed class PackManifestTests
{
    [TestMethod]
    public async Task BuildManifest_ScansModsAndConfigs()
    {
        var root = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "mods"));
            Directory.CreateDirectory(Path.Combine(root, "config"));
            await File.WriteAllBytesAsync(Path.Combine(root, "mods", "a.jar"), [1, 2, 3]);
            await File.WriteAllBytesAsync(Path.Combine(root, "mods", "b.jar"), [4, 5, 6]);
            await File.WriteAllTextAsync(Path.Combine(root, "config", "foo.toml"), "hello");
            await File.WriteAllTextAsync(Path.Combine(root, "config", "secret_token.toml"), "nope");

            var builder = new PackManifestBuilder(new DelegatePackSourceResolver((path, _, _) =>
                Task.FromResult<PackSourceInfo?>(Path.GetFileName(path) == "a.jar"
                    ? new PackSourceInfo
                    {
                        Source = PackConstants.SourceModrinth,
                        ProjectId = "project-a",
                        VersionId = "version-a"
                    }
                    : null)));

            var manifest = await builder.BuildAsync(new PackBuildOptions
            {
                InstancePath = root,
                MinecraftVersion = "1.21.1",
                Loader = "neoforge",
                LoaderVersion = "21.1.80"
            });

            Assert.AreEqual(2, manifest.Mods.Count);
            Assert.AreEqual(PackConstants.SourceModrinth, manifest.Mods.Single(m => m.FileName == "a.jar").Source);
            Assert.AreEqual(PackConstants.SourcePrivate, manifest.Mods.Single(m => m.FileName == "b.jar").Source);
            Assert.AreEqual(1, manifest.Configs.Count);
            Assert.AreEqual("config/foo.toml", manifest.Configs[0].Path);
            Assert.IsFalse(string.IsNullOrWhiteSpace(manifest.PackHash));
            Assert.IsTrue(PackManifestVerifier.VerifyHash(manifest));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [TestMethod]
    public void Hash_IsStable_WhenModOrderChanges()
    {
        var modA = new PackMod
        {
            Source = PackConstants.SourceModrinth,
            ProjectId = "a",
            VersionId = "1",
            Sha256 = "aaaa",
            FileName = "a.jar"
        };
        var modB = new PackMod
        {
            Source = PackConstants.SourcePrivate,
            Sha256 = "bbbb",
            FileName = "b.jar"
        };

        var first = new PackManifest
        {
            MinecraftVersion = "1.21.1",
            Loader = "neoforge",
            LoaderVersion = "21.1.80",
            Mods = [modA, modB]
        };
        var second = first with { Mods = [modB, modA] };

        Assert.AreEqual(
            PackManifestHasher.ComputeHash(first),
            PackManifestHasher.ComputeHash(second));
    }

    [TestMethod]
    public void VerifyHash_DetectsTampering()
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
                    Sha256 = "aaaa",
                    FileName = "a.jar"
                }
            ]
        };
        manifest = manifest with { PackHash = PackManifestHasher.ComputeHash(manifest) };

        Assert.IsTrue(PackManifestVerifier.VerifyHash(manifest));

        var tampered = manifest with { MinecraftVersion = "1.20.1" };
        Assert.IsFalse(PackManifestVerifier.VerifyHash(tampered));
    }

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
}
