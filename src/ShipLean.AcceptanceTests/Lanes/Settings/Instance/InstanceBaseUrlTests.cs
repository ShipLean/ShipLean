using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Instance;

// The seeded base URL is not the address the test host serves, so the page warns until the admin fixes it.
[TestFixture]
public class InstanceBaseUrlTests : SeededAcceptanceTest
{
    [Test]
    public async Task Instance_WhenBaseUrlDiffers_ShouldWarn()
    {
        var general = new InstanceGeneralPage(Page);
        await general.OpenAsync();

        await Expect(general.UrlMismatch).ToContainTextAsync(BaseUrl);
        await Expect(general.UrlMatch).ToHaveCountAsync(0);

        await general.BaseUrl.FillAsync(BaseUrl);
        await Expect(general.UrlMatch).ToBeVisibleAsync();
        await Expect(general.UrlMismatch).ToHaveCountAsync(0);
        await general.SaveAsync();
        await Page.ReloadAsync();

        await Expect(general.BaseUrl).ToHaveValueAsync(BaseUrl);
        await Expect(general.UrlMatch).ToBeVisibleAsync();
    }
}
