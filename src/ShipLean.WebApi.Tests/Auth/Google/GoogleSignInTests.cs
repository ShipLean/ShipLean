using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Features.Auth.Google;

namespace ShipLean.WebApi.Tests.Auth.Google;

[TestFixture]
public class GoogleSignInTests : GoogleSignInFixture
{
    [Test]
    public async Task Instance_WhenGoogleIsOn_ShouldTellTheSignInPage()
    {
        using var browser = Browser();

        var instance = await browser.GetFromJsonAsync<JsonNode>("/api/auth/instance");

        instance!["googleSignIn"]!.GetValue<bool>().ShouldBeTrue();
        instance["signIn"]!.GetValue<string>().ShouldBe("password");
    }

    [Test]
    public async Task Start_WhenCalled_ShouldSendTheBrowserToGoogleForThisClient()
    {
        using var browser = Browser();

        var query = await StartAsync(browser);

        query["client_id"].ToString().ShouldBe(ClientId);
        query["response_type"].ToString().ShouldBe("code");
        query.ShouldNotContainKey("response_mode"); // query is the code flow's default, so Google answers with a top-level GET
        query["scope"].ToString().ShouldContain("email");
        query["code_challenge_method"].ToString().ShouldBe("S256");
    }

    [TestCase("https://plan.example.test", "https://plan.example.test")]
    [TestCase("https://plan.example.test/some/path", "https://plan.example.test")]
    [TestCase("", null)]
    public async Task Start_WhenBaseUrlIs_ShouldUseItOrElseThisHostForTheCallback(string baseUrl, string? expected)
    {
        await using var connection = await OpenDatabaseAsync();
        var before = await connection.ExecuteScalarAsync<string>("SELECT base_url FROM instance_settings");
        await connection.ExecuteAsync("UPDATE instance_settings SET base_url = @baseUrl", new { baseUrl });
        try
        {
            using var browser = Browser();

            var query = await StartAsync(browser);

            query["redirect_uri"].ToString().ShouldBe((expected ?? Host.BaseUrl.TrimEnd('/')) + GoogleSignInServices.CallbackPath);
        }
        finally
        {
            await connection.ExecuteAsync("UPDATE instance_settings SET base_url = @before", new { before });
        }
    }

    [Test]
    public async Task SignIn_WhenEmailIsNew_ShouldCreateTheUserAndOpenTheReturnUrl()
    {
        using var browser = Browser();

        using var response = await SignInAsync(browser, "new-subject", "new.person@example.test", "New Person");

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location!.ToString().ShouldBe("/p/SHIP");
        var me = await MeAsync(browser);
        me["email"]!.GetValue<string>().ShouldBe("new.person@example.test");
        me["name"]!.GetValue<string>().ShouldBe("New Person");
        me["status"]!.GetValue<string>().ShouldBe("googleOnly");
    }

    [Test]
    public async Task SignIn_WhenGoogleSignsIn_ShouldTrackGoogleMethod()
    {
        using var browser = Browser();

        using var response = await SignInAsync(browser, "method-subject", "method.person@example.test");

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        await using var connection = await OpenDatabaseAsync();
        var userId = await connection.ExecuteScalarAsync<Guid>("SELECT id FROM app_user WHERE email = @email", new { email = "method.person@example.test" });
        var recorder = (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();
        var tracked = recorder.Events.Where(e => e.Name == UsageEventNames.AuthSignedIn && e.Actor.UserId == userId).ShouldHaveSingleItem();
        tracked.Source.ShouldBe("system:GoogleSignInEvents");
        tracked.Properties[UsageProps.Method].ShouldBe(SignInMethods.Google);
    }

    [Test]
    public async Task SignIn_WhenExchangingTheCode_ShouldSendTheClientSecretAndTheSameCallback()
    {
        using var browser = Browser();

        using var response = await SignInAsync(browser, "exchange-subject", "exchange@example.test");

        Provider.LastTokenRequest!["client_secret"].ShouldBe(ClientSecret);
        Provider.LastTokenRequest["redirect_uri"].ShouldBe((await StartAsync(browser))["redirect_uri"].ToString());
        Provider.LastTokenRequest["code_verifier"].ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task SignIn_WhenEmailHasAPasswordAccount_ShouldLinkItAndKeepPasswordAndName()
    {
        await using var connection = await OpenDatabaseAsync();
        await new AppUserBuilder().WithEmail("has.password@example.test").WithName("Chosen Name").InsertAsync(connection);
        using var browser = Browser();

        using var response = await SignInAsync(browser, "link-subject", "Has.Password@example.test", "Name At Google");

        var me = await MeAsync(browser);
        me["email"]!.GetValue<string>().ShouldBe("has.password@example.test");
        me["name"]!.GetValue<string>().ShouldBe("Chosen Name");
        me["status"]!.GetValue<string>().ShouldBe("active");
        using var withPassword = Browser();
        using var login = await withPassword.LoginAsync("has.password@example.test", AppUserBuilder.ArrangedPassword);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task SignIn_WhenLinkedAndEmailChangedAtGoogle_ShouldBeTheSameUser()
    {
        using var first = Browser();
        using var firstSignIn = await SignInAsync(first, "moved-subject", "before.move@example.test");
        var id = (await MeAsync(first))["id"]!.GetValue<string>();
        using var second = Browser();

        using var secondSignIn = await SignInAsync(second, "moved-subject", "after.move@example.test");

        var me = await MeAsync(second);
        me["id"]!.GetValue<string>().ShouldBe(id);
        me["email"]!.GetValue<string>().ShouldBe("before.move@example.test");
    }

    [Test]
    public async Task SignIn_WhenEmailIsLinkedToAnotherGoogleIdentity_ShouldRefuse()
    {
        using var owner = Browser();
        using var ownerSignIn = await SignInAsync(owner, "first-owner", "recycled@example.test");
        using var browser = Browser();

        using var response = await SignInAsync(browser, "new-owner", "recycled@example.test");

        response.Headers.Location!.ToString().ShouldStartWith($"/login?state={GoogleSignInEvents.RefusedState}");
        await ShouldHaveNoSessionAsync(browser);
    }

    [Test]
    public async Task SignIn_WhenAccountIsDisabled_ShouldRefuse()
    {
        await using var connection = await OpenDatabaseAsync();
        await new AppUserBuilder().WithEmail("disabled@example.test").DisabledAt(TimeProvider.System.GetUtcNow()).InsertAsync(connection);
        using var browser = Browser();

        using var response = await SignInAsync(browser, "disabled-subject", "disabled@example.test");

        response.Headers.Location!.ToString().ShouldStartWith($"/login?state={GoogleSignInEvents.RefusedState}");
        await ShouldHaveNoSessionAsync(browser);
    }

    [Test]
    public async Task SignIn_WhenEmailIsNotVerified_ShouldRefuseAndCreateNobody()
    {
        using var browser = Browser();

        using var response = await SignInAsync(browser, "unverified-subject", "unverified@example.test", emailVerified: false);

        response.Headers.Location!.ToString().ShouldStartWith($"/login?state={GoogleSignInEvents.RefusedState}");
        await ShouldHaveNoSessionAsync(browser);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM app_user WHERE email = 'unverified@example.test'")).ShouldBe(0);
    }

    [Test]
    public async Task SignIn_WhenUserHasAOneTimePassword_ShouldEndItAndLetThemWork()
    {
        await using var connection = await OpenDatabaseAsync();
        await new AppUserBuilder().WithEmail("one.time@example.test").MustChangePassword().InsertAsync(connection);
        using var browser = Browser();

        using var response = await SignInAsync(browser, "one-time-subject", "one.time@example.test");

        (await MeAsync(browser))["status"]!.GetValue<string>().ShouldBe("googleOnly");
        using var projects = await browser.GetAsync("/api/projects");
        projects.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var withPassword = Browser();
        using var login = await withPassword.LoginAsync("one.time@example.test", AppUserBuilder.ArrangedPassword);
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task SignIn_WhenPasswordIsLockedOut_ShouldStillSignInAndLeaveTheLock()
    {
        await using var connection = await OpenDatabaseAsync();
        await new AppUserBuilder().WithEmail("locked@example.test").LockedUntil(TimeProvider.System.GetUtcNow().AddHours(1)).InsertAsync(connection);
        using var browser = Browser();

        using var response = await SignInAsync(browser, "locked-subject", "locked@example.test");

        (await MeAsync(browser))["status"]!.GetValue<string>().ShouldBe("locked");
    }

    [Test]
    public async Task SignIn_WhenTokenIsForAnotherApp_ShouldFailWithNoSession()
    {
        using var browser = Browser();

        using var response = await SignInAsync(browser, "other-app-subject", "other.app@example.test", audience: "another-client.apps.googleusercontent.com");

        Provider.LastTokenRequest.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldStartWith($"/login?state={GoogleSignInEvents.FailedState}");
        await ShouldHaveNoSessionAsync(browser);
    }

    [Test]
    public async Task SignIn_WhenNonceIsWrong_ShouldFailWithNoSession()
    {
        using var browser = Browser();

        using var response = await SignInAsync(browser, "nonce-subject", "nonce@example.test", nonce: "not-this-attempts-nonce");

        response.Headers.Location!.ToString().ShouldStartWith($"/login?state={GoogleSignInEvents.FailedState}");
        await ShouldHaveNoSessionAsync(browser);
    }

    [Test]
    public async Task Callback_WhenStateIsForged_ShouldFailWithNoSession()
    {
        using var browser = Browser();

        using var response = await browser.GetAsync($"{GoogleSignInServices.CallbackPath}?code=test-code&state=forged");

        response.Headers.Location!.ToString().ShouldStartWith($"/login?state={GoogleSignInEvents.FailedState}");
        await ShouldHaveNoSessionAsync(browser);
    }

    [Test]
    public async Task Callback_WhenPersonCancelsAtGoogle_ShouldReturnToSignInKeepingTheReturnUrl()
    {
        using var browser = Browser();
        var query = await StartAsync(browser, "/p/PAY");

        using var response = await browser.GetAsync($"{GoogleSignInServices.CallbackPath}?error=access_denied&state={Uri.EscapeDataString(query["state"].ToString())}");

        response.Headers.Location!.ToString().ShouldBe($"/login?state={GoogleSignInEvents.FailedState}&returnUrl=%2Fp%2FPAY");
    }

    [TestCase("https://evil.example/p/PAY")]
    [TestCase("//evil.example/p/PAY")]
    [TestCase("/\\evil.example/p/PAY")]
    public async Task SignIn_WhenReturnUrlIsNotLocal_ShouldOpenTheRoot(string returnUrl)
    {
        using var browser = Browser();

        using var response = await SignInAsync(browser, Guid.NewGuid().ToString("N"), $"{Guid.NewGuid():N}@example.test", returnUrl: returnUrl);

        response.Headers.Location!.ToString().ShouldBe("/");
    }

    [Test]
    public async Task Resolve_WhenCalledAtOnceForTheSameNewPerson_ShouldCreateOneUser()
    {
        var accounts = Host.Services.GetRequiredService<GoogleAccounts>();

        var ids = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => accounts.ResolveAsync("race-subject", "race@example.test", "Race", CancellationToken.None)));

        ids.Distinct().Count().ShouldBe(1);
        ids[0].ShouldNotBeNull();
    }
}
