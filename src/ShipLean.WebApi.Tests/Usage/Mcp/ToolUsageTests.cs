using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Usage.Mcp;

// What an MCP tool call leaves in IUsageEvents (the test host's RecordingUsageEvents).
[TestFixture]
public class ToolUsageTests : McpFixture
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    protected override bool WithProbeTools => false;

    private RecordingUsageEvents Usage => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    private Task<UsageEvent> NextAsync(string source) => Usage.WaitForAsync(e => e.Source == source, Wait);

    [Test]
    public async Task CaptureWork_WhenAgentCalls_ShouldTrackWorkCapturedWithAgentActorAndProject()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "capture_work", """{"project":"SHIP","items":[{"type":"task","title":"Usage capture"}]}""");

        reply.IsToolError().ShouldBeFalse();
        var tracked = await NextAsync("tool:capture_work");
        tracked.Name.ShouldBe(UsageEventNames.WorkCaptured);
        tracked.Surface.ShouldBe(UsageSurface.Mcp);
        tracked.Actor.Kind.ShouldBe(UsageActorKind.Agent);
        tracked.Actor.UserId.ShouldBe(TestUser.Joel.Id);
        Guid.TryParse(tracked.Actor.AgentId, out _).ShouldBeTrue();
        tracked.Actor.AgentName.ShouldBe("Test Agent");
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.ProjectCount.ShouldBe(1);
        tracked.Outcome.ShouldBe(UsageOutcome.Ok);
        tracked.ErrorCode.ShouldBeNull();
    }

    [Test]
    public async Task GetWorkItem_WhenNotFound_ShouldTrackErrorWithNotFoundCode()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        await CallToolAsync(token, "get_work_item", """{"item":"SHIP-999999"}""");

        var tracked = await NextAsync("tool:get_work_item");
        tracked.Name.ShouldBe(UsageEventNames.WorkItemViewed);
        tracked.Outcome.ShouldBe(UsageOutcome.Error);
        tracked.ErrorCode.ShouldBe("not_found");
    }

    [Test]
    public async Task ListProjects_WhenCalled_ShouldTrackWithNoProjectId()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        await CallToolAsync(token, "list_projects");

        var tracked = await NextAsync("tool:list_projects");
        tracked.Name.ShouldBe(UsageEventNames.ProjectsListed);
        tracked.Outcome.ShouldBe(UsageOutcome.Ok);
        tracked.ProjectId.ShouldBeNull();
        tracked.ProjectCount.ShouldBe(0);
    }

    [Test]
    public async Task ToolCall_WhenScopeRefused_ShouldTrackForbidden()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        await CallToolAsync(token, "capture_work", """{"project":"SHIP","items":[{"type":"task","title":"Refused"}]}""");

        var tracked = await NextAsync("tool:capture_work");
        tracked.Outcome.ShouldBe(UsageOutcome.Error);
        tracked.ErrorCode.ShouldBe("forbidden");
    }

    [Test]
    public async Task ToolCall_ShouldRecordDuration()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        await CallToolAsync(token, "list_projects");

        var tracked = await NextAsync("tool:list_projects");
        tracked.DurationMs.ShouldNotBeNull();
        tracked.DurationMs.Value.ShouldBeGreaterThanOrEqualTo(0);
    }
}
