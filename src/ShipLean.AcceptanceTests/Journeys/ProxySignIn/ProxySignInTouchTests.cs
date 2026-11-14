using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Journeys.ProxySignIn;

// Behind the identity proxy on a phone, when no sign-in reached ShipLean (SHIP-736 L736a).
[TestFixture]
public class ProxySignInTouchTests : ProxyAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task ProxyMessage_WhenNoSignInReachedOnAPhone_ShouldShowTryAgainFullWidth()
    {
        // No token header: the proxy's identity never arrives.
        await Page.Context.SetExtraHTTPHeadersAsync(new Dictionary<string, string>());
        await Page.GotoAsync("/login");

        var message = Page.GetByTestId(AuthTestIds.ProxyMessage);
        await Expect(message).ToBeVisibleAsync();
        var card = (await Page.GetByTestId(AuthTestIds.ProxySignIn).BoundingBoxAsync())!;
        var retry = (await Page.GetByTestId(AuthTestIds.ProxyRetry).BoundingBoxAsync())!;
        var text = (await message.BoundingBoxAsync())!;
        retry.Width.ShouldBe(card.Width - 34f, 1f);
        retry.Height.ShouldBe(40f, 1f);
        retry.Y.ShouldBeGreaterThanOrEqualTo(text.Y + text.Height);
        (await TouchRules.ViolationsAsync(Page, Page.GetByTestId(AuthTestIds.ProxySignIn))).ShouldBeEmpty();
    }
}
