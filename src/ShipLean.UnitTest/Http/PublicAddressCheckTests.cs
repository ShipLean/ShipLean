using System.Net;
using ShipLean.Application.Http;

namespace ShipLean.UnitTest.Http;

public sealed class PublicAddressCheckTests
{
    [TestCase("127.0.0.1")]
    [TestCase("10.1.2.3")]
    [TestCase("172.16.0.1")]
    [TestCase("172.31.255.255")]
    [TestCase("192.168.1.1")]
    [TestCase("169.254.169.254")]
    [TestCase("100.64.0.1")]
    [TestCase("0.0.0.0")]
    [TestCase("224.0.0.1")]
    [TestCase("255.255.255.255")]
    [TestCase("::1")]
    [TestCase("::")]
    [TestCase("fe80::1")]
    [TestCase("fc00::1")]
    [TestCase("fd12:3456::1")]
    [TestCase("ff02::1")]
    [TestCase("::ffff:127.0.0.1")]
    [TestCase("::ffff:10.0.0.1")]
    [TestCase("::ffff:169.254.169.254")]
    [TestCase("::10.0.0.1")]
    [TestCase("64:ff9b::7f00:1")]
    [TestCase("2002:7f00:1::1")]
    [TestCase("2001::1")]
    [TestCase("2001:0:4136:e378:8000:63bf:3fff:fdd2")]
    [TestCase("2001:db8::1")]
    [TestCase("3fff::1")]
    [TestCase("100::1")]
    [TestCase("64:ff9b:1:7f00:0:100::")]
    [TestCase("64:ff9b::a9fe:a9fe")]
    [TestCase("64:ff9b:2::1")]
    [TestCase("64:ff9b:1:808:8:800::")]
    [TestCase("64:ff9b:1::808:808")]
    [TestCase("4000::1")]
    [TestCase("192.0.2.1")]
    [TestCase("192.0.2.255")]
    [TestCase("198.51.100.7")]
    [TestCase("203.0.113.200")]
    [TestCase("192.88.99.1")]
    [TestCase("::ffff:203.0.113.5")]
    [TestCase("2002:c000:0201::1")]
    public void IsAllowed_WhenNotPublic_ShouldRefuse(string address)
    {
        new PublicAddressCheck().IsAllowed(IPAddress.Parse(address)).ShouldBeFalse();
    }

    [TestCase("93.184.216.34")]
    [TestCase("172.32.0.1")]
    [TestCase("8.8.8.8")]
    [TestCase("2606:2800:220:1:248:1893:25c8:1946")]
    [TestCase("::ffff:8.8.8.8")]
    [TestCase("2a00:1450:4009::1")]
    [TestCase("64:ff9b::808:808")]
    [TestCase("2002:0808:0808::1")]
    [TestCase("192.0.3.1")]
    [TestCase("192.88.98.1")]
    [TestCase("198.51.101.1")]
    [TestCase("203.0.114.1")]
    public void IsAllowed_WhenPublic_ShouldAllow(string address)
    {
        new PublicAddressCheck().IsAllowed(IPAddress.Parse(address)).ShouldBeTrue();
    }
}
