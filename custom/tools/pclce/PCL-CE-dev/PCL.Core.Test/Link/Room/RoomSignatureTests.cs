using System;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Link.Protocol;

namespace PCL.Core.Test.Link.Room;

[TestClass]
public sealed class RoomSignatureTests
{
    [TestMethod]
    public void SignAndVerify_RoundTrips()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var record = CreateRecord(key);
        var signed = record with { Signature = RoomSignature.Sign(record, key) };

        Assert.IsTrue(RoomSignature.Verify(signed));
        Assert.AreEqual(16, RoomSignature.Fingerprint(signed.HostPublicKey).Length);
    }

    [TestMethod]
    public void Verify_RejectsTamperedRecord()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var record = CreateRecord(key);
        var signed = record with { Signature = RoomSignature.Sign(record, key) };
        var tampered = signed with { Name = "tampered" };

        Assert.IsFalse(RoomSignature.Verify(tampered));
    }

    [TestMethod]
    public void VerifyDelete_RejectsOldTimestamp()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var old = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var signature = RoomSignature.SignDelete("room", old, key);

        Assert.IsFalse(RoomSignature.VerifyDelete("room", old, publicKey, signature));
    }

    private static RoomRecord CreateRecord(ECDsa key) => new()
    {
        RoomId = "room-1",
        Name = "Test Room",
        MinecraftVersion = "1.21.1",
        Loader = "neoforge",
        LoaderVersion = "21.1.0",
        PackHash = "abc",
        Players = 1,
        Tags = ["survival", "modded"],
        JoinCode = "ABCDE",
        HostName = "host",
        HostPublicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
        UpdatedAt = DateTimeOffset.UtcNow,
        ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(90)
    };
}
