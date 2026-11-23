using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link.Pack;

namespace PCL.Core.Test.Link.Pack;

[TestClass]
public sealed class PackManifestSignerTests
{
    [TestMethod]
    public void SignAndVerify_RoundTrips()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var manifest = CreateManifest();
        var signed = PackManifestSigner.Sign(manifest, key);

        Assert.IsTrue(PackManifestSigner.VerifySignature(signed));
        Assert.IsFalse(string.IsNullOrWhiteSpace(signed.Signature));
        Assert.IsFalse(string.IsNullOrWhiteSpace(signed.Host?.PublicKey));
    }

    [TestMethod]
    public void Verify_RejectsChangedMinecraftVersion()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signed = PackManifestSigner.Sign(CreateManifest(), key);
        var tampered = signed with { MinecraftVersion = "1.20.1" };

        Assert.IsFalse(PackManifestSigner.VerifySignature(tampered));
    }

    private static PackManifest CreateManifest() => new()
    {
        MinecraftVersion = "1.21.1",
        Loader = "neoforge",
        LoaderVersion = "21.1.0",
        Host = new PackHost { Name = "host" }
    };
}
