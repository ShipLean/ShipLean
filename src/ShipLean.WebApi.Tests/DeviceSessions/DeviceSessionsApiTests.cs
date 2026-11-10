using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Identity;
using ShipLean.Contracts.DeviceSessions;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.Contracts.Users;
using ShipLean.TestSupport;
using ShipLean.TestSupport.DeviceSessions;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Features.DeviceSessions;
using ShipLean.WebApi.Tests.Auth;

namespace ShipLean.WebApi.Tests.DeviceSessions;

// The five device-session endpoints and the thin bearer path, through the real host with real sign-in: the browser is a
// cookie client (AuthClient), the phone a bearer client (DeviceBearerClient). No test sign-in, so a bearer request is
// authenticated by DeviceBearer and nothing else.
[TestFixture]
public class DeviceSessionsApiTests : ApiFixture
{
    private const string Base = "/api/auth/device-sessions";
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    private HttpClient _browser = null!;

    protected override bool UseTestSignIn => false;

    protected override TestUser SignedInAs => TestUser.Anonymous;

    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [OneTimeSetUp]
    public async Task SignInTheBrowser() => _browser = await AuthClient.SignedInAsync(Host, "joel@shiplean.local", SeedIds.Passwords.Joel);

    [OneTimeTearDown]
    public void DisposeTheBrowser() => _browser?.Dispose();

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    private static Task<HttpResponseMessage> PostCodesAsync(HttpClient client, string? challenge = Challenge, string? deviceName = "Joel's iPad")
        => client.PostAsJsonAsync($"{Base}/codes", new CreateSignInCodeRequest(challenge, deviceName));

    private async Task<string> CodeAsync()
    {
        using var response = await PostCodesAsync(_browser);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<SignInCodeDto>(AuthClient.Json))!.Code;
    }

    private async Task<HttpResponseMessage> ExchangeAsync(string code, string verifier)
    {
        using var phone = AuthClient.Create(Host);
        return await phone.PostAsJsonAsync($"{Base}/exchange", new ExchangeSignInCodeRequest(code, verifier));
    }

    [Test]
    public async Task Codes_WhenCookieSession_ShouldReturnCode()
    {
        var code = await CodeAsync();

        code.Length.ShouldBe(43);
        Recorder.Events.ShouldBeEmpty();
    }

    [TestCase("too-short", "Phone", "challenge")]
    [TestCase(null, "Phone", "challenge")]
    [TestCase("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw+cM", "Phone", "challenge")]
    [TestCase(Challenge, "   ", "deviceName")]
    [TestCase(Challenge, null, "deviceName")]
    public async Task Codes_WhenChallengeOrNameInvalid_ShouldReturnValidation(string? challenge, string? deviceName, string field)
    {
        using var response = await PostCodesAsync(_browser, challenge, deviceName);

        var error = await response.ReadApiErrorAsync(HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe(field);
    }

    [Test]
    public async Task Codes_WhenNameIs101Characters_ShouldReturnValidation()
    {
        using var response = await PostCodesAsync(_browser, deviceName: new string('a', 101));

        (await response.ReadApiErrorAsync(HttpStatusCode.BadRequest)).Field.ShouldBe("deviceName");
    }

    [Test]
    public async Task Codes_WhenDeviceBearer_ShouldReturnForbidden()
    {
        using var phone = await DeviceBearerClient.CreateAsync(Host, SeedIds.Users.Joel);

        using var response = await PostCodesAsync(phone.Client);

        (await response.ReadApiErrorAsync(HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
    }

    [Test]
    public async Task Exchange_WhenCodeAndVerifierMatch_ShouldReturnSessionSecretAndMe()
    {
        var code = await CodeAsync();

        using var response = await ExchangeAsync(code, Verifier);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var body = (await response.Content.ReadFromJsonAsync<DeviceSignInDto>(AuthClient.Json))!;
        DeviceSecret.HasShape(body.Secret).ShouldBeTrue();
        body.Session.Name.ShouldBe("Joel's iPad");
        body.Session.TokenPrefix.ShouldBe(body.Secret[..16]);
        body.Session.Current.ShouldBeFalse();
        body.Session.LastUsedLabel.ShouldBe("just now");
        body.Me.Id.ShouldBe(SeedIds.Users.Joel);
    }

    [Test]
    public async Task Exchange_WhenVerifierWrong_ShouldReturnSignInExpired()
    {
        var code = await CodeAsync();

        using var wrong = await ExchangeAsync(code, "wrong-verifier-wrong-verifier-wrong-verifier-wrong");
        using var retry = await ExchangeAsync(code, Verifier);

        var error = await wrong.ReadApiErrorAsync(HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Error.ShouldBe("Sign-in expired. Try again.");
        error.Field.ShouldBeNull();
        (await retry.ReadApiErrorAsync(HttpStatusCode.BadRequest)).Error.ShouldBe("Sign-in expired. Try again.");
    }

    [Test]
    public async Task Exchange_ShouldTrackCreated()
    {
        var code = await CodeAsync();

        using var response = await ExchangeAsync(code, Verifier);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.DeviceSessionCreated);
        tracked.Actor.ShouldBe(UsageActor.ForUser(SeedIds.Users.Joel));
        tracked.Surface.ShouldBe(UsageSurface.App);
    }

    [Test]
    public async Task Me_WhenBearerFromExchange_ShouldReturn200()
    {
        var code = await CodeAsync();
        using var exchanged = await ExchangeAsync(code, Verifier);
        var secret = (await exchanged.Content.ReadFromJsonAsync<DeviceSignInDto>(AuthClient.Json))!.Secret;
        using var phone = DeviceBearerClient.For(Host, secret);

        using var response = await phone.GetAsync("/api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<MeDto>(AuthClient.Json))!.Id.ShouldBe(SeedIds.Users.Joel);
    }

    // The 401 body is the cookie scheme's, reached through DeviceBearer's forwarded challenge.
    [Test]
    public async Task Me_WhenBearerRevoked_ShouldReturnSessionEnded()
    {
        using var phone = await DeviceBearerClient.CreateAsync(Host, SeedIds.Users.Joel);
        await Host.Services.GetRequiredService<IDeviceSessions>().RevokeAsync(SeedIds.Users.Joel, phone.Id, TimeProvider.System.GetUtcNow());

        using var response = await phone.Client.GetAsync("/api/me");

        (await response.ReadApiErrorAsync(HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.SessionEnded);
    }

    [Test]
    public async Task Me_WhenBearerExpired_ShouldReturnSessionEnded()
    {
        using var phone = await DeviceBearerClient.CreateAsync(
            Host, SeedIds.Users.Joel, now: TimeProvider.System.GetUtcNow() - DeviceSessionLimits.SessionLifetime - TimeSpan.FromMinutes(1));

        using var response = await phone.Client.GetAsync("/api/me");

        (await response.ReadApiErrorAsync(HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.SessionEnded);
    }

    [Test]
    public async Task Me_WhenBearerUnknown_ShouldReturnUnauthorized()
    {
        using var phone = DeviceBearerClient.For(Host, DeviceSecret.New());

        using var response = await phone.GetAsync("/api/me");

        (await response.ReadApiErrorAsync(HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.Unauthorized);
    }

    [Test]
    public async Task List_ShouldMarkCurrent()
    {
        using var first = await DeviceBearerClient.CreateAsync(Host, SeedIds.Users.Joel, "First phone");
        using var second = await DeviceBearerClient.CreateAsync(Host, SeedIds.Users.Joel, "Second phone");

        var fromFirst = (await first.Client.GetFromJsonAsync<List<DeviceSessionDto>>(Base, AuthClient.Json))!;
        var fromBrowser = (await _browser.GetFromJsonAsync<List<DeviceSessionDto>>(Base, AuthClient.Json))!;

        fromFirst.Single(s => s.Id == first.Id).Current.ShouldBeTrue();
        fromFirst.Single(s => s.Id == second.Id).Current.ShouldBeFalse();
        fromBrowser.ShouldAllBe(s => !s.Current);
        fromBrowser.Select(s => s.Id).ShouldContain(first.Id);
    }

    [Test]
    public async Task Revoke_WhenOwnSession_ShouldEndItAndTrackEndedWebSignOut()
    {
        using var phone = await DeviceBearerClient.CreateAsync(Host, SeedIds.Users.Joel);

        using var response = await _browser.DeleteAsync($"{Base}/{phone.Id}");
        using var after = await phone.Client.GetAsync("/api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await after.ReadApiErrorAsync(HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.SessionEnded);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.DeviceSessionEnded);
        tracked.Properties[UsageProps.Reason].ShouldBe(DeviceSessionEndReasons.WebSignOut);
        tracked.Surface.ShouldBe(UsageSurface.Web);
    }

    [Test]
    public async Task Revoke_ShouldTrackEndedWebSignOut()
    {
        using var phone = await DeviceBearerClient.CreateAsync(Host, SeedIds.Users.Joel);

        using var response = await _browser.DeleteAsync($"{Base}/{phone.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Recorder.Events.ShouldHaveSingleItem().Properties[UsageProps.Reason].ShouldBe(DeviceSessionEndReasons.WebSignOut);
    }

    [Test]
    public async Task Revoke_WhenAnotherUsersSession_ShouldReturn404()
    {
        using var priyasPhone = await DeviceBearerClient.CreateAsync(Host, SeedIds.Users.Priya);

        using var response = await _browser.DeleteAsync($"{Base}/{priyasPhone.Id}");
        using var priyaStill = await priyasPhone.Client.GetAsync("/api/me");

        (await response.ReadApiErrorAsync(HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        priyaStill.StatusCode.ShouldBe(HttpStatusCode.OK);
        Recorder.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task Current_WhenCookieRequest_ShouldReturn404()
    {
        using var response = await _browser.DeleteAsync($"{Base}/current");

        (await response.ReadApiErrorAsync(HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Current_ShouldTrackEndedAppSignOut()
    {
        using var phone = await DeviceBearerClient.CreateAsync(Host, SeedIds.Users.Joel);

        using var response = await phone.Client.DeleteAsync($"{Base}/current");
        using var after = await phone.Client.GetAsync("/api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await after.ReadApiErrorAsync(HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.SessionEnded);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.DeviceSessionEnded);
        tracked.Properties[UsageProps.Reason].ShouldBe(DeviceSessionEndReasons.AppSignOut);
        tracked.Surface.ShouldBe(UsageSurface.App);
    }

    // Outside /api the selector never sends the bearer to DeviceBearer: a page load with only a bearer is not signed in.
    [Test]
    public async Task PageLoad_WhenOnlyABearer_ShouldNotSignIn()
    {
        using var phone = await DeviceBearerClient.CreateAsync(Host, SeedIds.Users.Joel);

        using var response = await phone.Client.GetAsync("/profile");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }
}
