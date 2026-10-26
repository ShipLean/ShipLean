using ShipLean.Application.Features.Links.PublicFetch;

namespace ShipLean.UnitTest.Links;

// Which addresses one preview allow-list line opens: a bare host only its scheme's default port, host:port only that port.
public sealed class PreviewAllowEntryTests
{
    [TestCase("wiki.corp", "http://wiki.corp/page")]
    [TestCase("wiki.corp", "https://wiki.corp/page")]
    [TestCase("WIKI.corp", "https://wiki.CORP:443/page")]
    [TestCase("wiki.corp:8080", "http://wiki.corp:8080/page")]
    [TestCase("wiki.corp:8080", "https://wiki.corp:8080/page")]
    [TestCase("wiki.corp:80", "http://wiki.corp/page")]
    [TestCase("10.1.2.3:8443", "https://10.1.2.3:8443/")]
    [TestCase("[fd00::1]:8443", "https://[fd00::1]:8443/")]
    [TestCase("  wiki.corp  ", "http://wiki.corp/")]
    public void Allows_WhenHostAndPortMatch_ShouldBeTrue(string line, string address)
    {
        PreviewAllowEntry.Parse(line)!.Allows(new Uri(address)).ShouldBeTrue();
    }

    [TestCase("wiki.corp", "http://wiki.corp:6379/")]
    [TestCase("wiki.corp", "http://wiki.corp:22/")]
    [TestCase("wiki.corp", "https://wiki.corp:80/")]
    [TestCase("wiki.corp", "http://wiki.corp:443/")]
    [TestCase("wiki.corp:8080", "http://wiki.corp/")]
    [TestCase("wiki.corp:8080", "http://wiki.corp:8081/")]
    [TestCase("wiki.corp:80", "https://wiki.corp/")]
    [TestCase("wiki.corp", "http://wiki.corp.evil.example/")]
    [TestCase("wiki.corp", "http://sub.wiki.corp/")]
    [TestCase("[fd00::1]", "https://[fd00::1]:8443/")]
    public void Allows_WhenHostOrPortDiffers_ShouldBeFalse(string line, string address)
    {
        PreviewAllowEntry.Parse(line)!.Allows(new Uri(address)).ShouldBeFalse();
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("http://wiki.corp")]
    [TestCase("wiki.corp/path")]
    [TestCase("user@wiki.corp")]
    [TestCase("wiki.corp:notaport")]
    [TestCase("wiki.corp?x")]
    [TestCase("wiki.corp#x")]
    [TestCase("wiki.corp:")]
    public void Parse_WhenTheLineIsNotAHostOrHostAndPort_ShouldBeNull(string line)
    {
        PreviewAllowEntry.Parse(line).ShouldBeNull();
    }
}
