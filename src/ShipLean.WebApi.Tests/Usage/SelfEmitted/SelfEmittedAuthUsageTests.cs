using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Auth;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.Auth;

namespace ShipLean.WebApi.Tests.Usage.SelfEmitted;

// The password sign-in tracks auth.signed_in itself (the action filter leaves it alone), and the actions that only re-issue the cookie track nothing.
[TestFixture]
public class SelfEmittedAuthUsageTests : ApiFixture
{
    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    protected override TestUser SignedInAs => TestUser.Anonymous;

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    private async Task<(string Email, Guid UserId)> ArrangeUserAsync()
    {
        var builder = new AppUserBuilder();
        await using var connection = await OpenDatabaseAsync();
        return (builder.Email, await builder.InsertAsync(connection));
    }

    [Test]
    public async Task Login_WhenPasswordCorrect_ShouldTrackSignedInWithThatUser()
    {
        var (email, userId) = await ArrangeUserAsync();
        using var client = AuthClient.Create(Host);

        using var response = await client.LoginAsync(email, AppUserBuilder.ArrangedPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.AuthSignedIn);
        tracked.Surface.ShouldBe(UsageSurface.Web);
        tracked.Actor.ShouldBe(UsageActor.ForUser(userId));
        tracked.Outcome.ShouldBe(UsageOutcome.Ok);
        tracked.Source.ShouldBe("api:Auth.Login");
        tracked.DurationMs.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Login_WhenWrongPassword_ShouldTrackNothing()
    {
        var (email, _) = await ArrangeUserAsync();
        using var client = AuthClient.Create(Host);

        using var response = await client.LoginAsync(email, "not-the-password-1");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Recorder.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task ChangePassword_ShouldNotTrackSignedIn()
    {
        var (email, _) = await ArrangeUserAsync();
        using var client = await AuthClient.SignedInAsync(Host, email, AppUserBuilder.ArrangedPassword);
        Recorder.Clear();

        using var response = await client.PostAsJsonAsync("/api/auth/password", new ChangePasswordRequest(AppUserBuilder.ArrangedPassword, "a-brand-new-password"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Recorder.Events.ShouldNotContain(e => e.Name == UsageEventNames.AuthSignedIn);
    }
}
