using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Link.Protocol;

namespace PCL.RoomIndex.Tests;

[TestClass]
public sealed class RoomIndexApiTests
{
    [TestMethod]
    public async Task PublishSearchAndRejectTampered()
    {
        var db = Path.Combine(Path.GetTempPath(), "pcl-room-index-" + Guid.NewGuid().ToString("N") + ".db");
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("RoomIndex:DatabasePath", db));
        using var client = factory.CreateClient();

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var record = CreateRecord(key);

        var publish = await client.PutAsJsonAsync($"/v1/rooms/{record.RoomId}", record);
        Assert.AreEqual(HttpStatusCode.OK, publish.StatusCode, await publish.Content.ReadAsStringAsync());

        var rooms = await client.GetFromJsonAsync<List<RoomRecord>>("/v1/rooms?mc=1.21.1");
        Assert.IsNotNull(rooms);
        Assert.AreEqual(1, rooms.Count);
        Assert.AreEqual(record.RoomId, rooms[0].RoomId);

        var tampered = record with { Name = "tampered" };
        var rejected = await client.PutAsJsonAsync($"/v1/rooms/{tampered.RoomId}", tampered);
        Assert.AreEqual(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    private static RoomRecord CreateRecord(ECDsa key)
    {
        var now = DateTimeOffset.UtcNow;
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var record = new RoomRecord
        {
            RoomId = "room-" + Guid.NewGuid().ToString("N")[..8],
            Name = "Test Room",
            MinecraftVersion = "1.21.1",
            Loader = "neoforge",
            LoaderVersion = "21.1.0",
            PackHash = "abc",
            JoinCode = "ABCDE",
            HostName = "host",
            HostPublicKey = publicKey,
            UpdatedAt = now,
            ExpiresAt = now.AddSeconds(90)
        };
        return record with { Signature = RoomSignature.Sign(record, key) };
    }
}
