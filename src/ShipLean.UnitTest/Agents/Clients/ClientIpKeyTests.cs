using System.Net;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.UnitTest.Agents.Clients;

public sealed class ClientIpKeyTests
{
    [Test]
    public void For_WhenIPv4_ShouldBeTheAddress()
    {
        ClientIpKey.For(IPAddress.Parse("203.0.113.9")).ShouldBe("203.0.113.9");
    }

    [Test]
    public void For_WhenMappedIPv4_ShouldBeTheIPv4Address()
    {
        ClientIpKey.For(IPAddress.Parse("::ffff:203.0.113.9")).ShouldBe("203.0.113.9");
    }

    [Test]
    public void For_WhenTwoAddressesInOneIPv6Slash64_ShouldShareAKey()
    {
        ClientIpKey.For(IPAddress.Parse("2a00:1450:4009:81::1")).ShouldBe(ClientIpKey.For(IPAddress.Parse("2a00:1450:4009:81:aaaa:bbbb:cccc:dddd")));
        ClientIpKey.For(IPAddress.Parse("2a00:1450:4009:81::1")).ShouldNotBe(ClientIpKey.For(IPAddress.Parse("2a00:1450:4009:82::1")));
    }

    [Test]
    public void For_WhenUnknown_ShouldBeUnknown()
    {
        ClientIpKey.For((IPAddress?)null).ShouldBe("unknown");
    }
}
