using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Agents;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Contracts.Agents;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.Agents;

namespace ShipLean.WebApi.Tests.Usage.SelfEmitted;

// Consent, project creation and joining track their own events (the action filter leaves them alone).
[TestFixture]
public class SelfEmittedWebUsageTests : ApiFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => PinnedAgentFakes.Pin;

    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    private Guid Pending(TestUser user)
        => Host.Services.GetRequiredService<InMemoryAgentConsent>().Add(
            user.Id!.Value, InMemoryAgentStore.TestAgent.ClientId, "http://127.0.0.1:8123/callback", "st", AgentWorld.Challenge,
            ["shiplean:read", "shiplean:write"], AgentWorld.Resource, "https://ship.example.test");

    [Test]
    public async Task ConsentAllow_ShouldTrackAgentConnectedWithAgentName()
    {
        var id = Pending(TestUser.Priya);

        using var response = await Api.ActingAs(TestUser.Priya).PostJsonAsync($"api/agents/consent/{id}", new ConsentDecision(true, ["shiplean:write"]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.AgentConnected);
        tracked.Actor.ShouldBe(UsageActor.ForUser(TestUser.Priya.Id!.Value));
        tracked.Source.ShouldBe("api:Consent.Decide");
        tracked.Outcome.ShouldBe(UsageOutcome.Ok);
        tracked.Properties[UsageProps.AgentName].ShouldBe("Test Agent");
    }

    [Test]
    public async Task ConsentDeny_ShouldTrackConsentDenied()
    {
        var id = Pending(TestUser.Tom);

        using var response = await Api.ActingAs(TestUser.Tom).PostJsonAsync($"api/agents/consent/{id}", new ConsentDecision(false, []));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.AgentConsentDenied);
        tracked.Properties[UsageProps.AgentName].ShouldBe("Test Agent");
    }

    [Test]
    public async Task Consent_WhenUnknownRequest_ShouldTrackNothing()
    {
        using var response = await Api.PostJsonAsync($"api/agents/consent/{Guid.NewGuid()}", new ConsentDecision(true, []));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Recorder.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task CreateProject_ShouldTrackProjectCreatedWithNewProjectId()
    {
        using var response = await Api.PostJsonAsync("api/projects", new ProjectCreate("Tracked create", "TRK", null, [], [1, 2, 3, 4, 5], null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var project = (await response.Content.ReadFromJsonAsync<ProjectDto>(ApiJson))!;
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.ProjectCreated);
        tracked.ProjectId.ShouldBe(project.Id);
        tracked.ProjectCount.ShouldBe(1);
        tracked.Actor.ShouldBe(UsageActor.ForUser(SeedIds.Users.Joel));
        tracked.Source.ShouldBe("api:ProjectCreation.Create");
    }

    [Test]
    public async Task JoinProject_WhenAdminJoins_ShouldTrackProjectJoinedWithProjectId()
    {
        using var response = await Api.PostJsonAsync($"api/instance/projects/{SeedIds.Projects.Part}/join");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.ProjectJoined);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Part);
        tracked.ProjectCount.ShouldBe(1);
        tracked.Source.ShouldBe("api:Instance.Join");
    }

    [Test]
    public async Task JoinProject_WhenNotInstanceAdmin_ShouldTrackNothing()
    {
        using var response = await Api.ActingAs(TestUser.Mei).PostJsonAsync($"api/instance/projects/{SeedIds.Projects.Part}/join");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        Recorder.Events.ShouldBeEmpty();
    }

    private static global::System.Text.Json.JsonSerializerOptions ApiJson { get; } = new(global::System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new global::System.Text.Json.Serialization.JsonStringEnumConverter(global::System.Text.Json.JsonNamingPolicy.CamelCase) },
    };
}
