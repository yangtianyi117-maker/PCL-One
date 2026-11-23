using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link.Lan;

namespace PCL.Core.Test.Link.Lan;

[TestClass]
public class LanProtocolTests
{
    [TestMethod]
    public void Announce_UsesVanillaPayload()
    {
        Assert.AreEqual("[MOTD]PCL 定制版[/MOTD][AD]25565[/AD]", LanProtocol.Announce("PCL 定制版", 25565));
        Assert.AreEqual("[MOTD]PCL 定制版[/MOTD][AD]25565[/AD]", LanProtocol.Announce("", 25565));
    }

    [TestMethod]
    public void Sanitize_RemovesColorAndClosingTags()
    {
        Assert.AreEqual("房间 A", LanProtocol.Sanitize("§a房间 A"));
        Assert.AreEqual("abc", LanProtocol.Sanitize("abc[/MOTD][/AD]"));
        Assert.AreEqual("PCL 定制版", LanProtocol.Sanitize("   "));
    }

    [TestMethod]
    public void Reply_RoundTrips()
    {
        var reply = LanProtocol.Reply("我的房间", 25566, true);

        Assert.AreEqual("MCROOM1 25566 1 我的房间", reply);
        Assert.IsTrue(LanProtocol.TryParseReply(reply, IPAddress.Parse("192.168.1.3"), out var room));
        Assert.AreEqual("我的房间", room.Name);
        Assert.AreEqual(25566, room.Port);
        Assert.IsTrue(room.Official);
        Assert.AreEqual("192.168.1.3:25566", room.Address);
    }

    [TestMethod]
    public void TryParseReply_RejectsGarbage()
    {
        Assert.IsFalse(LanProtocol.TryParseReply(null, IPAddress.Loopback, out _));
        Assert.IsFalse(LanProtocol.TryParseReply("MCROOM1 x 0 name", IPAddress.Loopback, out _));
        Assert.IsFalse(LanProtocol.TryParseReply("MCROOM1 99999 0 name", IPAddress.Loopback, out _));
        Assert.IsFalse(LanProtocol.TryParseReply("hello", IPAddress.Loopback, out _));
    }

    [TestMethod]
    public void IsProbe_IsCaseInsensitiveAndTrimmed()
    {
        Assert.IsTrue(LanProtocol.IsProbe("MCROOM1?"));
        Assert.IsTrue(LanProtocol.IsProbe("  mcrooM1? "));
        Assert.IsFalse(LanProtocol.IsProbe("MCROOM1"));
    }

    [TestMethod]
    public void SubnetMath_IsConsistent()
    {
        Assert.AreEqual(24, LanProtocol.PrefixOf(IPAddress.Parse("255.255.255.0")));
        Assert.AreEqual(16, LanProtocol.PrefixOf(IPAddress.Parse("255.255.0.0")));
        Assert.AreEqual(
            IPAddress.Parse("192.168.1.255"),
            LanProtocol.BroadcastOf(IPAddress.Parse("192.168.1.3"), IPAddress.Parse("255.255.255.0")));

        Assert.IsTrue(LanProtocol.TryParseSubnet("10.0.8.0/24", out var broadcast, out var hosts));
        Assert.AreEqual(IPAddress.Parse("10.0.8.255"), broadcast);
        Assert.AreEqual(255, hosts.Count);
    }
}
