using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test.Link.Pack;

[TestClass]
public sealed class PackManifestFactoryTests
{
    [TestMethod]
    public void GetLoader_MapsNeoForge()
    {
        var (loader, version) = PackManifestFactory.GetLoader(new McInstanceInfo
        {
            HasNeoForge = true,
            NeoForge = "21.1.80"
        });

        Assert.AreEqual("neoforge", loader);
        Assert.AreEqual("21.1.80", version);
    }

    [TestMethod]
    public void GetLoader_MapsForge()
    {
        var (loader, version) = PackManifestFactory.GetLoader(new McInstanceInfo
        {
            HasForge = true,
            Forge = "47.2.0"
        });

        Assert.AreEqual("forge", loader);
        Assert.AreEqual("47.2.0", version);
    }

    [TestMethod]
    public void GetLoader_MapsFabric()
    {
        var (loader, version) = PackManifestFactory.GetLoader(new McInstanceInfo
        {
            HasFabric = true,
            Fabric = "0.16.9"
        });

        Assert.AreEqual("fabric", loader);
        Assert.AreEqual("0.16.9", version);
    }

    [TestMethod]
    public void GetLoader_FallsBackToVanilla()
    {
        var (loader, version) = PackManifestFactory.GetLoader(new McInstanceInfo());

        Assert.AreEqual("vanilla", loader);
        Assert.AreEqual(string.Empty, version);
    }
}
