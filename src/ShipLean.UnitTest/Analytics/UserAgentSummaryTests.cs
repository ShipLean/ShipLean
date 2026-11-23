using ShipLean.Application.Features.Analytics.PostHog;
using Shouldly;

namespace ShipLean.UnitTest.Analytics;

[TestFixture]
public class UserAgentSummaryTests
{
    private const string ChromeWindows = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
    private const string EdgeWindows = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0";
    private const string FirefoxLinux = "Mozilla/5.0 (X11; Linux x86_64; rv:133.0) Gecko/20100101 Firefox/133.0";
    private const string SafariMac = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Safari/605.1.15";
    private const string ChromeAndroidPhone = "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Mobile Safari/537.36";
    private const string ChromeAndroidTablet = "Mozilla/5.0 (Linux; Android 13; SM-X710) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
    private const string SafariIPhone = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Mobile/15E148 Safari/604.1";
    private const string SafariIPad = "Mozilla/5.0 (iPad; CPU OS 18_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Mobile/15E148 Safari/604.1";
    private const string Opera = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 OPR/115.0.0.0";

    [TestCase(ChromeWindows, "Chrome", "Windows", "Desktop")]
    [TestCase(EdgeWindows, "Microsoft Edge", "Windows", "Desktop")]
    [TestCase(FirefoxLinux, "Firefox", "Linux", "Desktop")]
    [TestCase(SafariMac, "Safari", "Mac OS X", "Desktop")]
    [TestCase(ChromeAndroidPhone, "Chrome", "Android", "Mobile")]
    [TestCase(ChromeAndroidTablet, "Chrome", "Android", "Tablet")]
    [TestCase(SafariIPhone, "Safari", "iOS", "Mobile")]
    [TestCase(SafariIPad, "Safari", "iOS", "Tablet")]
    public void Parse_WhenKnownUserAgent_ShouldNameBrowserOsAndDevice(string userAgent, string browser, string os, string device)
    {
        UserAgentSummary.Parse(userAgent).ShouldBe(new UserAgentSummary(browser, os, device));
    }

    [Test]
    public void Parse_WhenEdge_ShouldNotReportChrome()
    {
        UserAgentSummary.Parse(EdgeWindows).Browser.ShouldBe("Microsoft Edge");
    }

    [Test]
    public void Parse_WhenUnknownBrowser_ShouldOmitTheBrowser()
    {
        UserAgentSummary.Parse(Opera).Browser.ShouldBeNull();
    }

    [Test]
    public void Parse_WhenUnknownOs_ShouldOmitTheOs()
    {
        UserAgentSummary.Parse("curl/8.5.0").ShouldBe(new UserAgentSummary(null, null, "Desktop"));
    }
}
