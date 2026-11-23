using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Link.Protocol;
using PCL.RoomIndex;

namespace PCL.Core.Test.Link.Room;

[TestClass]
public sealed class RoomStoreTests
{
    [TestMethod]
    public void UpsertQueryAndExpire_Works()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-room-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new RoomStore(Path.Combine(root, "rooms.db"));
            store.Initialize();

            store.Upsert(new RoomRecord
            {
                RoomId = "room-1",
                Name = "Test",
                MinecraftVersion = "1.21.1",
                Loader = "neoforge",
                LoaderVersion = "21.1.0",
                PackHash = "abc",
                JoinCode = "AAAAA",
                HostName = "host",
                HostPublicKey = "key",
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(90)
            });

            Assert.AreEqual(1, store.Query(new RoomFilter()).Count);
            Assert.IsNotNull(store.Get("room-1"));

            store.Upsert(new RoomRecord
            {
                RoomId = "room-2",
                Name = "Expired",
                MinecraftVersion = "1.21.1",
                Loader = "vanilla",
                JoinCode = "BBBBB",
                HostName = "host",
                HostPublicKey = "key",
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1)
            });

            Assert.AreEqual(1, store.Query(new RoomFilter()).Count);
            Assert.AreEqual(1, store.Cleanup());
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (Exception) { /* best effort */ }
        }
    }
}
