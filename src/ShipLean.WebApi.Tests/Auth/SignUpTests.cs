using System.Net;
using System.Net.Http.Json;
using Dapper;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Auth;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Users;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth;

// POST /api/auth/signup with SHIPLEAN_SIGNUP_ENABLED on, through the real cookie.
[TestFixture]
public class SignUpTests : ApiFixture
{
    private const string Password = "a-reviewer-password";

    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        [SignUpService.EnabledKey] = "true",
    };

    [Test]
    public async Task SignUp_WhenValid_ShouldCreateThePasswordUserAndSignThemIn()
    {
        using var browser = AuthClient.Create(Host);

        using var response = await browser.PostAsJsonAsync("/api/auth/signup", new SignUpRequest(" Play Reviewer ", " reviewer@example.test ", Password));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var created = (await response.Content.ReadFromJsonAsync<MeDto>(AuthClient.Json))!;
        created.Email.ShouldBe("reviewer@example.test");
        created.Name.ShouldBe("Play Reviewer");
        using var me = await browser.GetAsync("/api/me");
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var again = AuthClient.Create(Host);
        using var login = await again.LoginAsync("reviewer@example.test", Password);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task SignUp_WhenEmailExistsInAnyCase_ShouldConflictAndLeaveTheUserAlone()
    {
        using var browser = AuthClient.Create(Host);

        using var response = await browser.PostAsJsonAsync("/api/auth/signup", new SignUpRequest("Not Priya", "PRIYA@shiplean.local", Password));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Error.ShouldBe(SignUpService.Taken);
        using var priya = await AuthClient.Create(Host).LoginAsync("priya@shiplean.local", SeedIds.Passwords.Priya);
        priya.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await priya.Content.ReadFromJsonAsync<MeDto>(AuthClient.Json))!.Name.ShouldNotBe("Not Priya");
    }

    [TestCase("Short Password", "short-pw@example.test", "too-short")]
    [TestCase("Bad Email", "not-an-email", Password)]
    [TestCase("", "no-name@example.test", Password)]
    public async Task SignUp_WhenInputIsInvalid_ShouldBeValidationAndCreateNobody(string name, string email, string password)
    {
        using var browser = AuthClient.Create(Host);

        using var response = await browser.PostAsJsonAsync("/api/auth/signup", new SignUpRequest(name, email, password));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM app_user WHERE lower(email) = lower(@email)", new { email })).ShouldBe(0);
    }

    [Test]
    public async Task SignUpPage_WhenSignedOut_ShouldNotRedirectToSignIn()
    {
        using var browser = AuthClient.Create(Host);

        using var page = await browser.GetAsync("/signup");
        using var control = await browser.GetAsync("/p/SHIP/backlogs/stories");

        page.Headers.Location.ShouldBeNull();
        control.Headers.Location!.AbsolutePath.ShouldBe("/login");
    }
}
