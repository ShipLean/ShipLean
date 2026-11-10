using System.Net;
using Dapper;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// Proxy mode refuses everything password-shaped with 403 forbidden, instance admin or not (K10). Joel, the seeded
// instance admin, is signed in by the test scheme; B's handler is not involved.
[TestFixture]
public class ProxyPasswordRefusalTests : ApiFixture
{
    protected override IReadOnlyDictionary<string, string?> HostSettings => ProxyApiFixture.LiteralTokenSettings;

    [Test]
    public async Task Login_WhenProxyMode_ShouldBeForbidden()
    {
        using var response = await Api.ActingAs(TestUser.Anonymous).PostJsonAsync("api/auth/login", new { email = "joel@shiplean.local", password = SeedIds.Passwords.Joel });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
    }

    [Test]
    public async Task ChangePassword_WhenProxyMode_ShouldBeForbidden()
    {
        using var response = await Api.PostJsonAsync("api/auth/password", new { current = SeedIds.Passwords.Joel, @new = "a-brand-new-password" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
    }

    [Test]
    public async Task OneTimePassword_WhenProxyModeAndCallerIsInstanceAdmin_ShouldBeForbidden()
    {
        using var response = await Api.PostJsonAsync($"api/admin/users/{SeedIds.Users.Priya}/one-time-password");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<bool>("SELECT must_change_password FROM app_user WHERE id = @id", new { id = SeedIds.Users.Priya })).ShouldBeFalse();
    }

    [Test]
    public async Task ChangeEmail_WhenProxyMode_ShouldBeForbidden()
    {
        using var response = await Api.PutJsonAsync($"api/admin/users/{SeedIds.Users.Priya}/email", new { email = "priya.changed@example.test" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<string>("SELECT email FROM app_user WHERE id = @id", new { id = SeedIds.Users.Priya })).ShouldNotBe("priya.changed@example.test");
    }
}
