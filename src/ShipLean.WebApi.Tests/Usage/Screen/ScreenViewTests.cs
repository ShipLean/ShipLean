using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Usage;

namespace ShipLean.WebApi.Tests.Usage.Screen;

// POST api/usage/screen: a known screen, and a project only for a member of it. Joel is a member of Ship and not of Part.
[TestFixture]
public class ScreenViewTests : ApiFixture
{
    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    [Test]
    public async Task ScreenView_WhenMember_ShouldTrackWithProjectAndScreen()
    {
        using var response = await Api.PostJsonAsync("api/usage/screen", new ScreenViewDto("taskboard", SeedIds.Projects.Ship));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.ScreenViewed);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.ProjectCount.ShouldBe(1);
        tracked.Surface.ShouldBe(UsageSurface.Web);
        tracked.Actor.ShouldBe(UsageActor.ForUser(SeedIds.Users.Joel));
        tracked.Source.ShouldBe("screen");
        tracked.Properties[UsageProps.Screen].ShouldBe("taskboard");
    }

    [Test]
    public async Task ScreenView_WhenNoProject_ShouldTrackWithoutProject()
    {
        using var response = await Api.PostJsonAsync("api/usage/screen", new ScreenViewDto("profile", null));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.ScreenViewed);
        tracked.ProjectId.ShouldBeNull();
        tracked.ProjectCount.ShouldBe(0);
        tracked.Properties[UsageProps.Screen].ShouldBe("profile");
    }

    [Test]
    public async Task ScreenView_WhenBacklogMode_ShouldTrackTheMode()
    {
        using var response = await Api.PostJsonAsync("api/usage/screen", new ScreenViewDto("featuresBacklog", SeedIds.Projects.Ship, "tree"));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Properties[UsageProps.Screen].ShouldBe("featuresBacklog");
        tracked.Properties[UsageProps.Mode].ShouldBe("tree");
    }

    [Test]
    public async Task ScreenView_WhenNoMode_ShouldTrackNoMode()
    {
        using var response = await Api.PostJsonAsync("api/usage/screen", new ScreenViewDto("taskboard", SeedIds.Projects.Ship));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Recorder.Events.ShouldHaveSingleItem().Properties.Keys.ShouldBe([UsageProps.Screen]);
    }

    [TestCase("")]
    [TestCase("board")]
    public async Task ScreenView_WhenModeInvalid_ShouldBeValidation(string mode)
    {
        using var response = await Api.PostJsonAsync("api/usage/screen", new ScreenViewDto("featuresBacklog", SeedIds.Projects.Ship, mode));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await response.ReadApiErrorAsync();
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("mode");
        Recorder.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task ScreenView_WhenNotMember_ShouldBeNotFoundAndTrackNothing()
    {
        using var response = await Api.PostJsonAsync("api/usage/screen", new ScreenViewDto("taskboard", SeedIds.Projects.Part));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Recorder.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task ScreenView_WhenNotMemberAndScreenInvalid_ShouldBeNotFoundAndTrackNothing()
    {
        using var response = await Api.PostJsonAsync("api/usage/screen", new ScreenViewDto("notARoute", SeedIds.Projects.Part));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadApiErrorAsync()).Code.ShouldBe(ApiErrorCodes.NotFound);
        Recorder.Events.ShouldBeEmpty();
    }

    [TestCase("")]
    [TestCase("a-b")]
    [TestCase("<script>")]
    [TestCase("notARoute")]
    public async Task ScreenView_WhenScreenInvalid_ShouldBeValidation(string screen)
    {
        using var response = await Api.PostJsonAsync("api/usage/screen", new ScreenViewDto(screen, null));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await response.ReadApiErrorAsync();
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("screen");
        Recorder.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task ScreenView_WhenSignedOut_ShouldBe401()
    {
        using var response = await Api.ActingAs(TestUser.Anonymous).PostJsonAsync("api/usage/screen", new ScreenViewDto("profile", null));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Recorder.Events.ShouldBeEmpty();
    }
}
