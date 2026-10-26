using System.Net;
using System.Net.Http.Json;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Users;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth;

// POST /api/auth/login against the seeded accounts, with the real cookie.
[TestFixture]
public class SignInTests : ApiFixture
{
    protected override TestUser SignedInAs => TestUser.Anonymous;

    [Test]
    public async Task Login_WhenValid_ShouldSetCookie()
    {
        using var client = AuthClient.Create(Host);

        using var response = await client.LoginAsync("priya@shiplean.local", SeedIds.Passwords.Priya);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cookie = response.Headers.GetValues("Set-Cookie").Single();
        cookie.ShouldStartWith("shiplean.auth=");
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("samesite=lax", Case.Insensitive);
        (await response.Content.ReadFromJsonAsync<MeDto>(AuthClient.Json))!.Id.ShouldBe(SeedIds.Users.Priya);
        using var me = await client.GetAsync("/api/me");
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Login_WhenValid_ShouldAcceptTheEmailInAnyCase()
    {
        using var client = AuthClient.Create(Host);

        using var response = await client.LoginAsync("  Priya@ShipLean.Local ", SeedIds.Passwords.Priya);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Login_WhenWrongPassword_ShouldReturnOneGenericError()
    {
        using var client = AuthClient.Create(Host);

        using var wrongPassword = await client.LoginAsync("tom@shiplean.local", "not-his-password");
        using var wrongEmail = await client.LoginAsync("nobody@shiplean.local", "not-his-password");

        var first = await ApiClient.ReadApiErrorAsync(wrongPassword, HttpStatusCode.Unauthorized);
        var second = await ApiClient.ReadApiErrorAsync(wrongEmail, HttpStatusCode.Unauthorized);
        first.Code.ShouldBe(ApiErrorCodes.Unauthorized);
        first.Error.ShouldBe(SignInService.Refusal);
        second.ShouldBe(first);
    }

    [Test]
    public async Task Login_WhenNoPassword_ShouldReturnOneGenericError()
    {
        (await RefusalAsync("dana@shiplean.local", "anything-at-all-1")).Error.ShouldBe(SignInService.Refusal);
    }

    [Test]
    public async Task Login_WhenLocked_ShouldReturnOneGenericError()
    {
        (await RefusalAsync("jo@shiplean.local", SeedIds.Passwords.Jo)).Error.ShouldBe(SignInService.Refusal);
    }

    [Test]
    public async Task Login_WhenDisabled_ShouldReturnOneGenericError()
    {
        (await RefusalAsync("lee@shiplean.local", SeedIds.Passwords.Lee)).Error.ShouldBe(SignInService.Refusal);
    }

    [Test]
    public async Task Login_WhenOneTimePassword_ShouldRequirePasswordChange()
    {
        using var client = AuthClient.Create(Host);

        using var login = await client.LoginAsync("ravi@shiplean.local", SeedIds.Passwords.Ravi);
        using var me = await client.GetAsync("/api/me");
        using var logout = await client.PostAsync("/api/auth/logout", null);

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await login.Content.ReadFromJsonAsync<MeDto>(AuthClient.Json))!.Status.ShouldBe(UserStatus.OneTimePassword);
        var error = await ApiClient.ReadApiErrorAsync(me, HttpStatusCode.Forbidden);
        error.Code.ShouldBe(ApiErrorCodes.PasswordChangeRequired);
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Login_WhenBodyIsEmpty_ShouldReturnTheGenericError()
    {
        using var client = AuthClient.Create(Host);

        using var response = await client.LoginAsync(string.Empty, string.Empty);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Unauthorized)).Error.ShouldBe(SignInService.Refusal);
    }

    private async Task<ApiError> RefusalAsync(string email, string password)
    {
        using var client = AuthClient.Create(Host);

        using var response = await client.LoginAsync(email, password);

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Unauthorized);
        error.Code.ShouldBe(ApiErrorCodes.Unauthorized);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        return error;
    }
}
