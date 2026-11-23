using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link;

namespace PCL.Core.Test.Link;

[TestClass]
public sealed class BroadcastLocalTests
{
    [TestMethod]
    public async Task AdvertisesRoomOnMinecraftLanMulticast()
    {
        const int advertisedPort = 25565;

        using var listener = new BroadcastListener(receiveLocalOnly: false);
        var received = new TaskCompletionSource<BroadcastRecord?>(TaskCreationOptions.RunContinuationsAsynchronously);
        listener.OnReceive += (record, _) =>
        {
            if (record.Address.Port == advertisedPort)
                received.TrySetResult(record);
        };
        listener.Start();

        using var broadcaster = new BroadcastLocal("PCL 定制版 测试房间", advertisedPort);
        broadcaster.Start();

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(7)));
        Assert.AreSame(received.Task, completed, "7 秒内没有收到 Minecraft LAN 广播");

        var record = await received.Task;
        Assert.IsNotNull(record);
        StringAssert.Contains(record.Desc, "PCL 定制版");
        Assert.AreEqual(advertisedPort, record.Address.Port);
    }
}
