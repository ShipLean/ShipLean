using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Agents;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Agents;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Lanes.Agent.Screens;

// The consent page: what the person is asked and what Allow and Deny send back. Joel is signed in.
[TestFixture]
public class ConsentTests : SeededAcceptanceTest
{
    private const string Loopback = "http://127.0.0.1:8123/callback";
    private const string Callback = "http://127.0.0.1:8123";

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<IAgentAddresses, FixedAgentAddresses>();

    [Test]
    public async Task Consent_WhenScopesUnticked_ShouldGrantOnlyTicked()
    {
        var client = await PendingConsent.RegisterClientAsync(Host, "Untick Bot", Loopback);
        var requestId = await PendingConsent.CreateAsync(Host, TestUser.Joel, client.ClientId, Loopback, "shiplean:read", "shiplean:write", "shiplean:plan");
        var consent = new ConsentScreen(Page);
        await consent.AnswerCallbackAsync(Callback);
        await consent.OpenAsync(requestId);

        await Expect(consent.ScopePlan).ToBeCheckedAsync();
        await consent.ScopePlan.UncheckAsync();
        await consent.Allow.ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex("^" + Regex.Escape(Loopback) + @"\?.*code="));
        Query().Get("state").ShouldBe(PendingConsent.State);
        Query().Get("iss").ShouldNotBeNullOrEmpty();
        var granted = (await PendingConsent.ConnectedAsync(Host, TestUser.Joel)).Where(agent => agent.ClientName == "Untick Bot").ShouldHaveSingleItem();
        granted.Scopes.ShouldBe(["shiplean:read", "shiplean:write"], ignoreOrder: true);
    }

    [Test]
    public async Task Consent_WhenDenied_ShouldRedirectWithAccessDenied()
    {
        var client = await PendingConsent.RegisterClientAsync(Host, "Deny Bot", Loopback);
        var requestId = await PendingConsent.CreateAsync(Host, TestUser.Joel, client.ClientId, Loopback, "shiplean:read", "shiplean:write");
        var consent = new ConsentScreen(Page);
        await consent.AnswerCallbackAsync(Callback);
        await consent.OpenAsync(requestId);

        await consent.Deny.ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex("^" + Regex.Escape(Loopback) + @"\?.*error=access_denied"));
        Query().Get("code").ShouldBeNull();
        Query().Get("state").ShouldBe(PendingConsent.State);
        Query().Get("iss").ShouldNotBeNullOrEmpty();
        (await PendingConsent.ConnectedAsync(Host, TestUser.Joel)).ShouldNotContain(agent => agent.ClientName == "Deny Bot");
    }

    [Test]
    public async Task Consent_WhenLocalhostOnlyRedirect_ShouldWarn()
    {
        var local = await PendingConsent.RegisterClientAsync(Host, "Local Bot", Loopback);
        var localRequest = await PendingConsent.CreateAsync(Host, TestUser.Joel, local.ClientId, Loopback, "shiplean:read");
        var consent = new ConsentScreen(Page);
        await consent.OpenAsync(localRequest);
        await Expect(consent.LocalhostWarning).ToBeVisibleAsync();

        var hosted = await PendingConsent.RegisterClientAsync(Host, "Hosted Bot", Loopback, "https://agent.example.test/callback");
        var hostedRequest = await PendingConsent.CreateAsync(Host, TestUser.Joel, hosted.ClientId, "https://agent.example.test/callback", "shiplean:read");
        await consent.OpenAsync(hostedRequest);
        await Expect(consent.RedirectHost).ToHaveTextAsync("agent.example.test");
        await Expect(consent.LocalhostWarning).ToHaveCountAsync(0);
    }

    private Uri CurrentUrl => new(Page.Url);

    private System.Collections.Specialized.NameValueCollection Query() => HttpUtility.ParseQueryString(CurrentUrl.Query);
}
