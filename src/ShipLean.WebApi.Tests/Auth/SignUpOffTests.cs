using System.Net;
using System.Net.Http.Json;
using Dapper;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Auth;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth;

// Sign-up with SHIPLEAN_SIGNUP_ENABLED set to anything but true.
[TestFixture]
public class SignUpOffTests : ApiFixture
{
    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        [SignUpService.EnabledKey] = "yes",
    };

    [Test]
    public async Task SignUp_WhenNotTrue_ShouldBeNotFoundAndCreateNobody()
    {
        using var browser = AuthClient.Create(Host);

        using var response = await browser.PostAsJsonAsync("/api/auth/signup", new SignUpRequest("Play Reviewer", "off@example.test", "a-reviewer-password"));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Error.ShouldBe(SignUpService.Off);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM app_user WHERE email = 'off@example.test'")).ShouldBe(0);
    }
}
