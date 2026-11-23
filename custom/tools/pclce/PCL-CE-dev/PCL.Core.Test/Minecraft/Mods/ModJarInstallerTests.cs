using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL;

namespace PCL.Core.Test.Minecraft.Mods;

[TestClass]
public sealed class ModJarInstallerTests
{
    [TestMethod]
    public async Task Install_FabricJar_DetectsAndCopies()
    {
        var root = CreateTempDirectory();
        try
        {
            var jar = Path.Combine(root, "example.jar");
            CreateZip(jar, ("fabric.mod.json",
                """
                {"schemaVersion":1,"id":"examplemod","name":"Example","version":"1.0.0",
                 "environment":"client","depends":{"minecraft":">=1.21 <1.22","fabricloader":">=0.15"}}
                """));

            var instance = Path.Combine(root, "instance");
            Directory.CreateDirectory(instance);
            var results = await ModJarInstaller.InstallAsync([jar], instance, "1.21.1", "Fabric");

            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].Success, results[0].Message);
            Assert.AreEqual("Fabric", results[0].Info?.Loader);
            Assert.IsTrue(File.Exists(Path.Combine(instance, "mods", "example.jar")));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [TestMethod]
    public async Task Install_RejectsWrongMinecraftVersion()
    {
        var root = CreateTempDirectory();
        try
        {
            var jar = Path.Combine(root, "old.jar");
            CreateZip(jar, ("fabric.mod.json",
                """{"schemaVersion":1,"id":"old","version":"1.0.0","depends":{"minecraft":"1.20.1"}}"""));

            var instance = Path.Combine(root, "instance");
            Directory.CreateDirectory(instance);
            var results = await ModJarInstaller.InstallAsync([jar], instance, "1.21.1", "Fabric");

            Assert.IsFalse(results[0].Success);
            StringAssert.Contains(results[0].Message, "MC 版本不匹配");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [TestMethod]
    public async Task Install_RejectsWrongLoader()
    {
        var root = CreateTempDirectory();
        try
        {
            var jar = Path.Combine(root, "forge.jar");
            CreateZip(jar, ("META-INF/mods.toml",
                """
                modLoader="javafml"
                [[mods]]
                modId="forgeexample"
                version="1.0.0"
                displayName="Forge Example"
                """));

            var instance = Path.Combine(root, "instance");
            Directory.CreateDirectory(instance);
            var results = await ModJarInstaller.InstallAsync([jar], instance, "1.21.1", "Fabric");

            Assert.IsFalse(results[0].Success);
            StringAssert.Contains(results[0].Message, "加载器不匹配");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void CreateZip(string path, params (string Name, string Content)[] entries)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            var entry = archive.CreateEntry(name);
            using var stream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pcl-mod-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        try { Directory.Delete(path, true); }
        catch (Exception) { /* best effort */ }
    }
}
