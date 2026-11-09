using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Auth;
using ShipLean.Contracts.DeviceSessions;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.Contracts.Users;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.DeviceSessions;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.Auth;
using ShipLean.WebApi.Tests.Realtime;

namespace ShipLean.WebApi.Tests.DeviceSessions.Bearer;

// The device bearer, hardened (password mode): where it counts, where it never does, and what ends a session. No test
// sign-in, so a request is authenticated by DeviceBearer or the cookie scheme and nothing else. Each user is made for
// the test, so one test's stamp change never reaches another.
[TestFixture]
[CancelAfter(60000)]
public class DeviceBearerApiTests : ApiFixture
{
    private const string NewPassword = "a-brand-new-password";
    private static readonly TimeSpan Session = TimeSpan.FromDays(90);

    protected override bool UseTestSignIn => false;

    protected override TestUser SignedInAs => TestUser.Anonymous;

    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    private IDeviceSessions Store => Host.Services.GetRequiredService<IDeviceSessions>();

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    [Test]
    public async Task Api_WhenDeviceBearer_ShouldAuthenticateAsUser()
    {
        var (userId, _) = await ArrangeUserAsync();
        using var phone = await DeviceBearerClient.CreateAsync(Host, userId);

        using var response = await phone.Client.GetAsync("/api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<MeDto>(AuthClient.Json))!.Id.ShouldBe(userId);
        using var sessions = await phone.Client.GetAsync("/api/auth/device-sessions");
        (await sessions.Content.ReadFromJsonAsync<List<DeviceSessionDto>>(AuthClient.Json))!.Single(s => s.Current).Id.ShouldBe(phone.Id);
    }

    [Test]
    public async Task Hub_WhenAccessTokenQuery_ShouldConnect()
    {
        var (userId, _) = await ArrangeUserAsync(member: true);
        using var phone = await DeviceBearerClient.CreateAsync(Host, userId);

        // The hub is WebSockets only, so SignalR sends the token as `access_token` in the query.
        await using var receiver = await HubReceiver.StartAsync(
            Host, configure: options => options.AccessTokenProvider = () => Task.FromResult<string?>(phone.SignIn.Secret));
        await receiver.Connection.InvokeAsync("JoinProject", SeedIds.Projects.Ship);

        receiver.Connection.State.ShouldBe(HubConnectionState.Connected);
    }

    [Test]
    public async Task Hub_WhenAccessTokenQueryRevoked_ShouldNotConnect()
    {
        var (userId, _) = await ArrangeUserAsync(member: true);
        using var phone = await DeviceBearerClient.CreateAsync(Host, userId);
        (await Store.RevokeAsync(userId, phone.Id, DateTimeOffset.UtcNow)).ShouldBeTrue();

        await Should.ThrowAsync<HttpRequestException>(() => HubReceiver.StartAsync(
            Host, configure: options => options.AccessTokenProvider = () => Task.FromResult<string?>(phone.SignIn.Secret)));
    }

    [Test]
    public async Task Api_WhenAccessTokenQuery_ShouldNotAuthenticate()
    {
        var (userId, _) = await ArrangeUserAsync();
        using var phone = await DeviceBearerClient.CreateAsync(Host, userId, now: DateTimeOffset.UtcNow.AddHours(-2));
        var before = await Store.FindBySecretAsync(phone.SignIn.Secret);
        using var plain = AuthClient.Create(Host);

        using var withToken = await plain.GetAsync($"/api/me?access_token={phone.SignIn.Secret}");
        using var without = await plain.GetAsync("/api/me");

        (await Describe(withToken)).ShouldBe(await Describe(without));
        withToken.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Store.FindBySecretAsync(phone.SignIn.Secret)).ShouldBe(before);
    }

    [Test]
    public async Task Api_WhenMcpAccessToken_ShouldReturn401()
    {
        using var client = AuthClient.Create(Host);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {DeviceSecret.New().Replace("shl_dev_", "shl_mcp_")}");

        using var response = await client.GetAsync("/api/me");

        (await response.ReadApiErrorAsync(HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.Unauthorized);
    }

    // A device bearer on a path an agent reaches with its own credentials is answered as if no Authorization header had
    // been sent (several of these are anonymous and answer 400 or 404, not 401), and the session is not touched.
    [TestCase("GET", "/mcp")]
    [TestCase("POST", "/oauth/token")]
    [TestCase("POST", "/linear/graphql")]
    [TestCase("PUT", "/api/uploads/not-a-live-token")]
    [TestCase("GET", "/api/downloads/not-a-live-token")]
    public async Task RefusedPath_WhenDeviceBearer_ShouldAnswerAsWithoutAuthorization(string method, string path)
    {
        var (userId, _) = await ArrangeUserAsync();
        using var phone = await DeviceBearerClient.CreateAsync(Host, userId, now: DateTimeOffset.UtcNow.AddHours(-2));
        var before = await Store.FindBySecretAsync(phone.SignIn.Secret);
        using var plain = AuthClient.Create(Host);

        using var with = await phone.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        using var without = await plain.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        (await Describe(with)).ShouldBe(await Describe(without));
        (await Store.FindBySecretAsync(phone.SignIn.Secret)).ShouldBe(before);
    }

    [Test]
    public async Task Write_WhenDeviceBearerAndNoCookie_ShouldPass()
    {
        var (userId, _) = await ArrangeUserAsync();
        using var phone = await DeviceBearerClient.CreateAsync(Host, userId);
        using var other = await DeviceBearerClient.CreateAsync(Host, userId, "Other phone");

        using var response = await phone.Client.DeleteAsync($"/api/auth/device-sessions/{other.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Api_WhenMustChangePasswordAndStampMatches_ShouldReturnPasswordChangeRequired()
    {
        var (userId, _) = await ArrangeUserAsync(mustChange: true);
        using var phone = await DeviceBearerClient.CreateAsync(Host, userId);

        using var response = await phone.Client.GetAsync("/api/projects");

        (await response.ReadApiErrorAsync(HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.PasswordChangeRequired);
    }

    [Test]
    public async Task Api_WhenUsedWithinWindow_ShouldSlide()
    {
        var (userId, _) = await ArrangeUserAsync();
        var now = DateTimeOffset.UtcNow;
        using var phone = await DeviceBearerClient.CreateAsync(Host, userId, now: now.AddDays(-89));
        (await Store.FindBySecretAsync(phone.SignIn.Secret))!.ExpiresAt.ShouldBe(now.AddDays(1), TimeSpan.FromMinutes(1));

        using var response = await phone.Client.GetAsync("/api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Store.FindBySecretAsync(phone.SignIn.Secret))!.ExpiresAt.ShouldBe(now + Session, TimeSpan.FromMinutes(1));
    }

    [Test]
    public async Task Api_WhenSessionRevoked_ShouldReturnSessionEnded()
    {
        var (userId, _) = await ArrangeUserAsync();
        using var phone = await DeviceBearerClient.CreateAsync(Host, userId);
        (await Store.RevokeAsync(userId, phone.Id, DateTimeOffset.UtcNow)).ShouldBeTrue();

        await ShouldBeEndedAsync(phone, reason: null);
    }

    [Test]
    public async Task Api_WhenSessionExpired_ShouldReturnSessionEnded()
    {
        var (userId, _) = await ArrangeUserAsync();
        using var phone = await DeviceBearerClient.CreateAsync(Host, userId, now: DateTimeOffset.UtcNow.AddDays(-91));

        await ShouldBeEndedAsync(phone, DeviceSessionEndReasons.Expired);
    }

    [Test]
    public async Task Api_WhenUserDisabled_ShouldReturnSessionEnded()
    {
        var (userId, _) = await ArrangeUserAsync();
        using var phone = await DeviceBearerClient.CreateAsync(Host, userId);
        using var admin = await AdminAsync();

        using var disabled = await admin.PutAsJsonAsync($"/api/admin/users/{userId}/disabled", new SetDisabledRequest(true));

        disabled.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ShouldBeEndedAsync(phone, DeviceSessionEndReasons.UserDisabled);
    }

    [Test]
    public async Task Api_WhenPasswordChanged_ShouldReturnSessionEnded()
    {
        var (userId, email) = await ArrangeUserAsync();
        using var phone = await DeviceBearerClient.CreateAsync(Host, userId);
        using var browser = await AuthClient.SignedInAsync(Host, email, AppUserBuilder.ArrangedPassword);

        using var changed = await browser.PostAsJsonAsync("/api/auth/password", new ChangePasswordRequest(AppUserBuilder.ArrangedPassword, NewPassword));

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ShouldBeEndedAsync(phone, DeviceSessionEndReasons.StampChanged);
    }

    [Test]
    public async Task Api_WhenSignOutEverywhereElse_ShouldReturnSessionEnded()
    {
        var (userId, email) = await ArrangeUserAsync();
        using var phone = await DeviceBearerClient.CreateAsync(Host, userId);
        using var browser = await AuthClient.SignedInAsync(Host, email, AppUserBuilder.ArrangedPassword);

        using var signedOut = await browser.PostAsync("/api/auth/sign-out-others", null);

        signedOut.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ShouldBeEndedAsync(phone, DeviceSessionEndReasons.StampChanged);
    }

    [Test]
    public async Task Api_WhenAdminSetsOneTimePassword_ShouldReturnSessionEnded()
    {
        var (userId, _) = await ArrangeUserAsync();
        using var phone = await DeviceBearerClient.CreateAsync(Host, userId);
        using var admin = await AdminAsync();

        using var issued = await admin.PostAsync($"/api/admin/users/{userId}/one-time-password", null);

        issued.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ShouldBeEndedAsync(phone, DeviceSessionEndReasons.StampChanged);
    }

    [Test]
    public async Task Api_WhenEmailChanged_ShouldReturnSessionEnded()
    {
        var (userId, _) = await ArrangeUserAsync();
        using var phone = await DeviceBearerClient.CreateAsync(Host, userId);
        using var admin = await AdminAsync();

        using var changed = await admin.PutAsJsonAsync($"/api/admin/users/{userId}/email", new ChangeEmailRequest($"moved-{Guid.NewGuid():N}@shiplean.local"));

        changed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ShouldBeEndedAsync(phone, DeviceSessionEndReasons.StampChanged);
    }

    // 401 session_ended on the next call and on the one after; device_session.ended (surface app) is tracked once, with
    // the reason, by the call that ended it. A revoked session ends with no event here: whoever revoked it tracked that.
    private async Task ShouldBeEndedAsync(DeviceBearerSession phone, string? reason)
    {
        for (var call = 0; call < 2; call++)
        {
            using var response = await phone.Client.GetAsync("/api/me");
            (await response.ReadApiErrorAsync(HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.SessionEnded);
        }
        var ended = Recorder.Events.Where(e => e.Name == UsageEventNames.DeviceSessionEnded).ToList();
        if (reason is null)
        {
            ended.ShouldBeEmpty();
            return;
        }
        var tracked = ended.ShouldHaveSingleItem();
        tracked.Properties[UsageProps.Reason].ShouldBe(reason);
        tracked.Surface.ShouldBe(UsageSurface.App);
    }

    private static async Task<(int Status, string Code)> Describe(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var code = string.Empty;
        try
        {
            code = JsonDocument.Parse(body).RootElement.TryGetProperty("code", out var c) ? c.GetString() ?? string.Empty : string.Empty;
        }
        catch (JsonException)
        {
        }
        return ((int)response.StatusCode, code);
    }

    private Task<HttpClient> AdminAsync() => AuthClient.SignedInAsync(Host, "joel@shiplean.local", SeedIds.Passwords.Joel);

    private async Task<(Guid UserId, string Email)> ArrangeUserAsync(bool mustChange = false, bool member = false)
    {
        var builder = new AppUserBuilder();
        if (mustChange)
        {
            builder.MustChangePassword();
        }
        await using var db = await OpenDatabaseAsync();
        var userId = await builder.InsertAsync(db);
        if (member)
        {
            await db.ExecuteAsync(
                "INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @projectId, @userId, 'member')",
                new { id = Guid.NewGuid(), projectId = SeedIds.Projects.Ship, userId });
        }
        return (userId, builder.Email);
    }
}
