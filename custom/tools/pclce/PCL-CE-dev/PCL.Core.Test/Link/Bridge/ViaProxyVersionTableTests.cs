using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link.Bridge;

namespace PCL.Core.Test.Link.Bridge;

[TestClass]
public sealed class ViaProxyVersionTableTests
{
    [TestMethod]
    public void Allowlist_HasAtLeastTenPopularVersions()
    {
        Assert.IsTrue(ViaProxyVersionTable.SupportedVersions.Count >= 10);
    }

    [TestMethod]
    public void Allowlist_ContainsExpectedBridges()
    {
        Assert.IsTrue(ViaProxyVersionTable.IsSupported("1.20.1"));
        Assert.IsTrue(ViaProxyVersionTable.IsSupported("1.21.1"));
        Assert.IsTrue(ViaProxyVersionTable.IsSupported("26.3"));
    }

    [TestMethod]
    public void Allowlist_RejectsUnverifiedVersions()
    {
        Assert.IsFalse(ViaProxyVersionTable.IsSupported("1.21.2"));
        Assert.IsFalse(ViaProxyVersionTable.IsSupported("1.21.7"));
        Assert.IsFalse(ViaProxyVersionTable.IsSupported("26.4-snapshot-10"));
        Assert.IsFalse(ViaProxyVersionTable.IsSupported(""));
    }
}
