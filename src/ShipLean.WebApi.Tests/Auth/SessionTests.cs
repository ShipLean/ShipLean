using System.Net;
using System.Net.Http.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Auth;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Users;
using ShipLean.WebApi.Features.Auth;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth;

// What ends a session, and what keeps one. The clock is pinned so the once-a-minute stamp re-check can be stepped over.
[TestFixture]
public class SessionTests : ApiFixture
{
    private const string NewPassword = "a-brand-new-password";
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));

    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<TimeProvider>(_clock);
        services.AddControllers().AddApplicationPart(typeof(AuthTestController).Assembly);
    };

    [Test]
    public async Task Me_WhenNoCookie_ShouldAnswer401()
    {
        using var client = AuthClient.Create(Host);

        using var response = await client.GetAsync("/api/me");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.Unauthorized);
    }

    [Test]
    public async Task Page_WhenSignedOut_ShouldRedirectToSignInWithTheReturnUrl()
    {
        using var client = AuthClient.Create(Host);

        using var response = await client.GetAsync("/p/PAY/backlogs/stories?x=1");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.PathAndQuery.ShouldBe("/login?returnUrl=%2Fp%2FPAY%2Fbacklogs%2Fstories%3Fx%3D1");
    }

    [Test]
    public async Task Page_WhenSessionEnded_ShouldRedirectToSignInWithTheEndedState()
    {
        var (email, userId) = await ArrangeUserAsync();
        using var client = await AuthClient.SignedInAsync(Host, email, AppUserBuilder.ArrangedPassword);
        await ExecuteAsync("UPDATE app_user SET security_stamp = 'rotated' WHERE id = @userId", new { userId });
        _clock.Advance(TimeSpan.FromSeconds(61));

        using var response = await client.GetAsync("/p/PAY/sprints?x=1");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        new Uri(new Uri(Host.BaseUrl), response.Headers.Location!).PathAndQuery.ShouldBe("/login?state=ended&returnUrl=%2Fp%2FPAY%2Fsprints%3Fx%3D1");
    }

    [Test]
    public async Task Me_WhenStampRotated_ShouldAnswerSessionEnded()
    {
        var (email, userId) = await ArrangeUserAsync();
        using var client = await AuthClient.SignedInAsync(Host, email, AppUserBuilder.ArrangedPassword);
        (await client.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await ExecuteAsync("UPDATE app_user SET security_stamp = 'rotated' WHERE id = @userId", new { userId });

        using var cached = await client.GetAsync("/api/me");
        _clock.Advance(TimeSpan.FromSeconds(61));
        using var ended = await client.GetAsync("/api/me");

        cached.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiClient.ReadApiErrorAsync(ended, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.SessionEnded);
    }

    [Test]
    public async Task Me_WhenDisabled_ShouldAnswerSessionEnded()
    {
        var (email, userId) = await ArrangeUserAsync();
        using var client = await AuthClient.SignedInAsync(Host, email, AppUserBuilder.ArrangedPassword);
        await ExecuteAsync("UPDATE app_user SET disabled_at = @now WHERE id = @userId", new { userId, now = _clock.GetUtcNow() });

        _clock.Advance(TimeSpan.FromSeconds(61));
        using var ended = await client.GetAsync("/api/me");

        (await ApiClient.ReadApiErrorAsync(ended, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.SessionEnded);
    }

    [Test]
    public async Task Me_WhenStampRotated_ShouldDeleteTheCookie()
    {
        var (email, userId) = await ArrangeUserAsync();
        using var client = await AuthClient.SignedInAsync(Host, email, AppUserBuilder.ArrangedPassword);
        await ExecuteAsync("UPDATE app_user SET security_stamp = 'rotated' WHERE id = @userId", new { userId });
        _clock.Advance(TimeSpan.FromSeconds(61));

        using var ended = await client.GetAsync("/api/me");

        (await ApiClient.ReadApiErrorAsync(ended, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.SessionEnded);
        ended.Headers.GetValues("Set-Cookie").ShouldContain(cookie => cookie.StartsWith(AuthServices.CookieName + "=;", StringComparison.Ordinal));
    }

    [Test]
    public async Task Session_WhenUserDisabled_ShouldEndOnTheNextRequest()
    {
        var (email, userId) = await ArrangeUserAsync();
        using var client = await AuthClient.SignedInAsync(Host, email, AppUserBuilder.ArrangedPassword);
        using var otherBrowser = await AuthClient.SignedInAsync(Host, email, AppUserBuilder.ArrangedPassword);
        (await client.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        Host.ActAs(TestUser.Joel.Id);
        await Host.Services.GetRequiredService<IUserAccounts>().SetDisabledAsync(userId, true);

        using var me = await client.GetAsync("/api/me");
        using var password = await otherBrowser.PostAsJsonAsync("/api/auth/password", new ChangePasswordRequest(AppUserBuilder.ArrangedPassword, NewPassword));

        (await ApiClient.ReadApiErrorAsync(me, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.SessionEnded);
        (await ApiClient.ReadApiErrorAsync(password, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.SessionEnded);
    }

    [Test]
    public async Task Me_WhenSessionEndedThenSignedInAgain_ShouldWork()
    {
        var (email, userId) = await ArrangeUserAsync();
        using var client = await AuthClient.SignedInAsync(Host, email, AppUserBuilder.ArrangedPassword);
        await ExecuteAsync("UPDATE app_user SET security_stamp = 'rotated' WHERE id = @userId", new { userId });
        _clock.Advance(TimeSpan.FromSeconds(61));
        (await client.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var login = await client.LoginAsync(email, AppUserBuilder.ArrangedPassword);
        using var me = await client.GetAsync("/api/me");

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Password_WhenChangeRequired_ShouldBeAllowed()
    {
        var (email, _) = await ArrangeUserAsync(mustChange: true);
        using var client = await AuthClient.SignedInAsync(Host, email, AppUserBuilder.ArrangedPassword);
        using var blocked = await client.GetAsync("/api/test-auth/anyone");

        using var changed = await client.PostAsJsonAsync("/api/auth/password", new ChangePasswordRequest(AppUserBuilder.ArrangedPassword, NewPassword));
        using var after = await client.GetAsync("/api/test-auth/anyone");

        (await ApiClient.ReadApiErrorAsync(blocked, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.PasswordChangeRequired);
        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        after.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var again = AuthClient.Create(Host);
        (await again.LoginAsync(email, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Password_WhenCurrentIsWrong_ShouldBeValidation()
    {
        var (email, _) = await ArrangeUserAsync();
        using var client = await AuthClient.SignedInAsync(Host, email, AppUserBuilder.ArrangedPassword);

        using var response = await client.PostAsJsonAsync("/api/auth/password", new ChangePasswordRequest("not-my-password", NewPassword));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Password_WhenChanged_ShouldEndOtherSessionsButNotMine()
    {
        var (email, _) = await ArrangeUserAsync();
        using var mine = await AuthClient.SignedInAsync(Host, email, AppUserBuilder.ArrangedPassword);
        using var other = await AuthClient.SignedInAsync(Host, email, AppUserBuilder.ArrangedPassword);

        using var changed = await mine.PostAsJsonAsync("/api/auth/password", new ChangePasswordRequest(AppUserBuilder.ArrangedPassword, NewPassword));
        using var myMe = await mine.GetAsync("/api/me");
        using var otherMe = await other.GetAsync("/api/me");

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        myMe.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiClient.ReadApiErrorAsync(otherMe, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.SessionEnded);
    }

    [Test]
    public async Task SignOutOthers_ShouldKeepMySession()
    {
        var (email, _) = await ArrangeUserAsync();
        using var mine = await AuthClient.SignedInAsync(Host, email, AppUserBuilder.ArrangedPassword);
        using var other = await AuthClient.SignedInAsync(Host, email, AppUserBuilder.ArrangedPassword);

        using var signedOut = await mine.PostAsync("/api/auth/sign-out-others", null);
        using var myMe = await mine.GetAsync("/api/me");
        using var otherMe = await other.GetAsync("/api/me");

        signedOut.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        myMe.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiClient.ReadApiErrorAsync(otherMe, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.SessionEnded);
    }

    [Test]
    public async Task Hub_WhenSessionEnded_ShouldKeepTheCookieUntilTheNextApiCall()
    {
        var (email, userId) = await ArrangeUserAsync();
        using var client = await AuthClient.SignedInAsync(Host, email, AppUserBuilder.ArrangedPassword);
        await ExecuteAsync("UPDATE app_user SET security_stamp = 'rotated' WHERE id = @userId", new { userId });
        _clock.Advance(TimeSpan.FromSeconds(61));

        using var negotiate = await client.PostAsync("/hubs/project/negotiate?negotiateVersion=1", null);
        using var me = await client.GetAsync("/api/me");

        (await ApiClient.ReadApiErrorAsync(negotiate, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.SessionEnded);
        DeletesTheCookie(negotiate).ShouldBeFalse();
        (await ApiClient.ReadApiErrorAsync(me, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.SessionEnded);
        DeletesTheCookie(me).ShouldBeTrue();
    }

    private static bool DeletesTheCookie(HttpResponseMessage response)
        => response.Headers.TryGetValues("Set-Cookie", out var values)
           && values.Any(v => v.StartsWith($"{AuthServices.CookieName}=;", StringComparison.Ordinal));

    private async Task<(string Email, Guid UserId)> ArrangeUserAsync(bool mustChange = false)
    {
        var builder = new AppUserBuilder();
        if (mustChange)
        {
            builder.MustChangePassword();
        }
        await using var connection = await OpenDatabaseAsync();
        return (builder.Email, await builder.InsertAsync(connection));
    }

    private async Task ExecuteAsync(string sql, object parameters)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(sql, parameters);
    }
}
