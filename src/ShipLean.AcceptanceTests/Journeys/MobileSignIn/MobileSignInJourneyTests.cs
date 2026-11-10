using System.Collections.Specialized;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Devices;
using ShipLean.AcceptanceTests.Pages.MobileSignIn;
using ShipLean.Contracts;
using ShipLean.Contracts.DeviceSessions;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.DeviceSessions;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Journeys.MobileSignIn;

// The browser handoff that signs the phone app in, against the real app: the confirm page, the code in the Open the app
// link (a browser reports no navigation to an unknown scheme, so the test reads that link's href), the exchange with the
// verifier, the bearer on the API, and ending it from Signed-in devices.
[TestFixture]
public class MobileSignInJourneyTests : SeededAcceptanceTest
{
    // RFC 7636 appendix B: the verifier and the S256 challenge made from it.
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    private static async Task<NameValueCollection> ConfirmAsync(MobileSignInPage confirm, string device, string state)
    {
        await confirm.OpenAsync(Challenge, state, device);
        await confirm.SignIn.ClickAsync();
        await Expect(confirm.Done).ToBeVisibleAsync();
        var href = await confirm.OpenApp.GetAttributeAsync("href");
        href.ShouldNotBeNull();
        href.ShouldStartWith("shiplean://signed-in?");
        return HttpUtility.ParseQueryString(href["shiplean://signed-in?".Length..]);
    }

    // The app's exchange call: anonymous, no cookie, with the client header.
    private async Task<HttpResponseMessage> ExchangeAsync(string code)
    {
        using var client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/device-sessions/exchange")
        {
            Content = JsonContent.Create(new ExchangeSignInCodeRequest(code, Verifier)),
        };
        request.Headers.Add(ClientHeader.Name, "acceptance-test");
        return await client.SendAsync(request);
    }

    [Test]
    [Order(1)]
    public async Task MobileSignIn_WhenConfirmedInBrowser_ShouldGiveTheAppABearer()
    {
        var confirm = new MobileSignInPage(Page);

        var redirect = await ConfirmAsync(confirm, "Joel's Pixel", "state-one");

        redirect["state"].ShouldBe("state-one");
        redirect["iss"].ShouldBe(BaseUrl.TrimEnd('/'));
        var code = redirect["code"];
        code.ShouldNotBeNullOrEmpty();
        using var exchange = await ExchangeAsync(code);
        exchange.StatusCode.ShouldBe(HttpStatusCode.Created);
        exchange.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        var signedIn = await exchange.Content.ReadFromJsonAsync<JsonElement>();
        signedIn.GetProperty("session").GetProperty("name").GetString().ShouldBe("Joel's Pixel");
        signedIn.GetProperty("me").GetProperty("id").GetGuid().ShouldBe(SeedIds.Users.Joel);

        using var bearer = DeviceBearerClient.For(Host, signedIn.GetProperty("secret").GetString()!);
        using var me = await bearer.GetAsync("/api/me");
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await me.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid().ShouldBe(SeedIds.Users.Joel);
        using var projects = await bearer.GetAsync("/api/projects");
        projects.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    [Order(2)]
    public async Task MobileSignIn_WhenSignedOutFromProfile_ShouldEndTheBearer()
    {
        var redirect = await ConfirmAsync(new MobileSignInPage(Page), "Kitchen tablet", "state-two");
        using var exchange = await ExchangeAsync(redirect["code"]!);
        exchange.StatusCode.ShouldBe(HttpStatusCode.Created);
        var secret = (await exchange.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("secret").GetString()!;
        using var bearer = DeviceBearerClient.For(Host, secret);
        using (var before = await bearer.GetAsync("/api/me"))
        {
            before.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var devices = new SignedInDevicesPanel(Page);
        await devices.OpenAsync();
        await Expect(devices.Row("Kitchen tablet")).ToBeVisibleAsync();
        await devices.SignOutAsync("Kitchen tablet");

        await Expect(Page.GetByTestId(ComponentTestIds.Toast).Filter(new LocatorFilterOptions { HasText = "Kitchen tablet is signed out." })).ToBeVisibleAsync();
        await Expect(devices.Row("Kitchen tablet")).ToHaveCountAsync(0);
        using var after = await bearer.GetAsync("/api/me");
        var error = await ApiClient.ReadApiErrorAsync(after, HttpStatusCode.Unauthorized);
        error.Code.ShouldBe(ApiErrorCodes.SessionEnded);
    }

    [Test]
    [Order(3)]
    public async Task MobileSignIn_WhenCancelled_ShouldIssueNoCode()
    {
        var codeRequests = new List<string>();
        Page.Request += (_, request) =>
        {
            if (request.Url.Contains("/api/auth/device-sessions/codes"))
            {
                codeRequests.Add(request.Url);
            }
        };
        var confirm = new MobileSignInPage(Page);
        await confirm.OpenAsync(Challenge, "state-three", "Spare phone");

        await confirm.Cancel.ClickAsync();

        await Expect(confirm.Cancelled).ToContainTextAsync("Nothing was signed in");
        codeRequests.ShouldBeEmpty();
    }

    [Test]
    [Order(4)]
    public async Task MobileSignIn_WhenCodeReplayed_ShouldRefuseSecond()
    {
        var redirect = await ConfirmAsync(new MobileSignInPage(Page), "Replay phone", "state-four");
        var code = redirect["code"]!;

        using var first = await ExchangeAsync(code);
        using var second = await ExchangeAsync(code);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        var error = await ApiClient.ReadApiErrorAsync(second, HttpStatusCode.BadRequest);
        error.Error.ShouldBe("Sign-in expired. Try again.");
    }
}
