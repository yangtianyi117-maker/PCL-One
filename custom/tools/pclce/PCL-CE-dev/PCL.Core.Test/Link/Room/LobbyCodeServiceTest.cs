using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link.Room;

namespace PCL.Core.Test.Link.Room;

[TestClass]
public class LobbyCodeServiceTest
{
    [TestMethod]
    public void Generate_ProducesParseableCode()
    {
        var code = LobbyCodeService.Generate();

        Assert.IsTrue(code.FullCode.StartsWith("U/"));
        Assert.IsTrue(LobbyCodeService.TryParse(code.FullCode, out var parsed));
        Assert.AreEqual(code.FullCode, parsed.FullCode);
        Assert.AreEqual(code.NetworkName, parsed.NetworkName);
        Assert.AreEqual(code.NetworkSecret, parsed.NetworkSecret);
    }

    [TestMethod]
    public void TryParse_NormalizesCaseSpacesAndHyphens()
    {
        var code = LobbyCodeService.Generate();
        var body = code.FullCode[2..];
        var noHyphens = "u/" + body.Replace("-", "").ToLowerInvariant();
        var spaced = "  " + code.FullCode.ToLowerInvariant().Replace("-", " - ") + "  ";

        Assert.IsTrue(LobbyCodeService.TryParse(noHyphens, out var a));
        Assert.IsTrue(LobbyCodeService.TryParse(spaced, out var b));
        Assert.AreEqual(code.FullCode, a.FullCode);
        Assert.AreEqual(code.FullCode, b.FullCode);
    }

    [TestMethod]
    public void Normalize_IsIdempotent()
    {
        var code = LobbyCodeService.Generate();
        var once = LobbyCodeService.Normalize(code.FullCode);
        var twice = LobbyCodeService.Normalize(once);

        Assert.AreEqual(once, twice);
        Assert.AreEqual(code.FullCode, once);
    }

    [TestMethod]
    public void TryParse_RejectsGarbage()
    {
        Assert.IsFalse(LobbyCodeService.TryParse("", out _));
        Assert.IsFalse(LobbyCodeService.TryParse("hello", out _));
        Assert.IsFalse(LobbyCodeService.TryParse("U/1234-5678", out _));
        Assert.IsFalse(LobbyCodeService.TryParse("ML-ABCD-EFGH", out _));
    }

    [TestMethod]
    public void IsLegacyCode_RecognizesOldFormats()
    {
        Assert.IsTrue(LobbyCodeService.IsLegacyCode("ML-ABCD-EFGH"));
        Assert.IsTrue(LobbyCodeService.IsLegacyCode("ML-PUBA-AAAA"));
        Assert.IsFalse(LobbyCodeService.IsLegacyCode(LobbyCodeService.Generate().FullCode));
    }
}
