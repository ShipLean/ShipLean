using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Journeys.ProxySignIn;

// A person behind the identity proxy: the browser carries the proxy's token on every request.
[TestFixture]
public class ProxySignInJourneyTests : ProxyAcceptanceTest
{
    // Every address the main frame visits, so a bounce through the sign-in page can't hide behind the final URL.
    private List<string> _visited = null!;

    [SetUp]
    public void RecordNavigations()
    {
        _visited = [];
        Page.FrameNavigated += (_, frame) =>
        {
            if (frame.ParentFrame == null)
            {
                _visited.Add(frame.Url);
            }
        };
    }

    private async Task OpenAsync(string path)
    {
        var response = await Page.GotoAsync(path);
        response.ShouldNotBeNull();
        response.Status.ShouldBe(200);
        response.Request.RedirectedFrom.ShouldBeNull();
    }

    [Test]
    [Order(1)]
    public async Task ProxySignIn_WhenTokenHeaderSent_ShouldLandInTheApp()
    {
        await OpenAsync("/");

        await Expect(Page.GetByTestId(ShellTestIds.UserButton)).ToBeVisibleAsync();
        await Page.GetByTestId(ShellTestIds.UserButton).ClickAsync();
        await Expect(Page.GetByTestId(ShellTestIds.UserMenuEmail)).ToHaveTextAsync(ProxyEmail);
        await Expect(Page.GetByTestId(ShellTestIds.UserMenuName)).ToHaveTextAsync(ProxyName);
        _visited.ShouldNotContain(url => url.Contains("/login"));
    }

    [Test]
    [Order(2)]
    public async Task ProxySignIn_WhenOnProfile_ShouldShowNoPasswordSection()
    {
        await OpenAsync(Links.Profile());

        await Expect(Page.GetByTestId(SettingsTestIds.ProfileHeading)).ToBeVisibleAsync();
        await Expect(Page.GetByTestId(SettingsTestIds.ProfileNameReadOnly)).ToBeVisibleAsync();
        await Expect(Page.GetByTestId(SettingsTestIds.PasswordPanel)).ToHaveCountAsync(0);
        _visited.ShouldNotContain(url => url.Contains("/login"));
    }

    [Test]
    [Order(3)]
    public async Task ProxySignIn_WhenOpeningLoginWithReturnUrl_ShouldLandOnThatPage()
    {
        await Page.GotoAsync("/login?returnUrl=" + Uri.EscapeDataString(Links.Profile()));

        await Expect(Page.GetByTestId(SettingsTestIds.ProfileHeading)).ToBeVisibleAsync();
        new Uri(Page.Url).AbsolutePath.ShouldBe(Links.Profile());
    }

    [Test]
    [Order(4)]
    public async Task ProxySignIn_WhenSigningOut_ShouldGoToTheProxySignOut()
    {
        await OpenAsync("/");
        await Page.GetByTestId(ShellTestIds.UserButton).ClickAsync();

        var logout = await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId(ShellTestIds.UserMenuSignOut).ClickAsync(),
            r => r.Url.EndsWith("/api/auth/logout"));

        logout.Ok.ShouldBeTrue();
        await Expect(Page.GetByTestId("idp-signed-out")).ToBeVisibleAsync();
    }
}
