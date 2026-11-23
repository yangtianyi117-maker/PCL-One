using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link.EasyTier;

namespace PCL.Core.Test;

/// <summary>
/// McLauncher 深度嵌入自建中继的回归测试：
/// 不管用户有没有配置自定义节点，自建节点都必须出现在列表首位，且不会重复。
/// </summary>
[TestClass]
public class McLauncherRelayTest
{
    [TestMethod]
    public void InjectsSelfHostedRelayWhenListEmpty()
    {
        var relays = new List<ETRelay>();
        ETController.InjectSelfHostedRelay(relays);

        Assert.AreEqual(1, relays.Count);
        Assert.AreEqual(ETController.SelfHostedRelay, relays[0].Url);
        Assert.AreEqual(ETRelayType.Custom, relays[0].Type);
    }

    [TestMethod]
    public void DoesNotDuplicateOnRepeatedInjection()
    {
        var relays = new List<ETRelay>();
        ETController.InjectSelfHostedRelay(relays);
        ETController.InjectSelfHostedRelay(relays);

        Assert.AreEqual(1, relays.Count, "重复注入应被去重");
    }

    [TestMethod]
    public void SelfHostedRelayGoesFirst()
    {
        var relays = new List<ETRelay>
        {
            new() { Url = "tcp://public.easytier.top:11010", Name = "Community", Type = ETRelayType.Community }
        };

        ETController.InjectSelfHostedRelay(relays);

        Assert.AreEqual(2, relays.Count);
        Assert.AreEqual(ETController.SelfHostedRelay, relays[0].Url, "自建中继必须排在最前");
    }
}
