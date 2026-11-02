using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Features.Mcp.Transport;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Usage.Mcp;

// What an MCP tool call leaves in IUsageEvents (the test host's RecordingUsageEvents).
[TestFixture]
public class ToolUsageTests : McpFixture
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    protected override bool WithProbeTools => false;

    private RecordingUsageEvents Usage => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearUsage() => Usage.Clear();

    private const string AgentClientName = "Usage Id Agent";

    private IEnumerable<UsageEvent> CallsOf(string source) => Usage.Events.Where(e => e.Source == source);

    private async Task<Guid> AuthorizationIdAsync(string clientName)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        var connected = await scope.ServiceProvider.GetRequiredService<IAgentAuthorizations>().ListAsync(TestUser.Joel.Id!.Value);
        return connected.Single(a => a.ClientName == clientName).Id;
    }

    private Task<UsageEvent> NextAsync(string source) => Usage.WaitForAsync(e => e.Source == source, Wait);

    [Test]
    public async Task CaptureWork_WhenAgentCalls_ShouldTrackWorkCapturedWithAgentActorAndProject()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite, AgentClientName);
        var authorizationId = await AuthorizationIdAsync(AgentClientName);

        var reply = await CallToolAsync(token, "capture_work", """{"project":"SHIP","items":[{"type":"task","title":"Usage capture"}]}""");

        reply.IsToolError().ShouldBeFalse();
        var tracked = await NextAsync("tool:capture_work");
        tracked.Name.ShouldBe(UsageEventNames.WorkCaptured);
        tracked.Surface.ShouldBe(UsageSurface.Mcp);
        tracked.Actor.Kind.ShouldBe(UsageActorKind.Agent);
        tracked.Actor.UserId.ShouldBe(TestUser.Joel.Id);
        tracked.Actor.AgentId.ShouldBe(authorizationId.ToString());
        tracked.Actor.DistinctId.ShouldBe($"agent:{authorizationId}");
        tracked.Actor.AgentName.ShouldBe(AgentClientName);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.ProjectCount.ShouldBe(1);
        tracked.Outcome.ShouldBe(UsageOutcome.Ok);
        tracked.ErrorCode.ShouldBeNull();
        CallsOf("tool:capture_work").ShouldHaveSingleItem();
    }

    [Test]
    public async Task GetWorkItem_WhenNotFound_ShouldTrackErrorWithNotFoundCode()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "get_work_item", """{"key":"SHIP-999999"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);

        var tracked = await NextAsync("tool:get_work_item");
        tracked.Name.ShouldBe(UsageEventNames.WorkItemViewed);
        tracked.Outcome.ShouldBe(UsageOutcome.Error);
        tracked.ErrorCode.ShouldBe("not_found");
        CallsOf("tool:get_work_item").ShouldHaveSingleItem();
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
        CallsOf("tool:list_projects").ShouldHaveSingleItem();
    }

    // The gate in front of the SDK refuses a missing scope with HTTP 403 before any filter runs, so nothing is tracked.
    // The filter's own forbidden answer is a backstop no real tool reaches (only test tools that disagree with the gate do).
    [Test]
    public async Task ToolCall_WhenGateRefusesScope_ShouldTrackNothing()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "capture_work", """{"project":"SHIP","plan":"not-a-real-plan"}""");

        reply.Status.ShouldBe(HttpStatusCode.Forbidden);
        Usage.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task ToolCall_ShouldRecordDuration()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        await CallToolAsync(token, "list_projects");

        var tracked = await NextAsync("tool:list_projects");
        tracked.DurationMs.ShouldNotBeNull();
        tracked.DurationMs.Value.ShouldBeGreaterThanOrEqualTo(0);
        CallsOf("tool:list_projects").ShouldHaveSingleItem();
    }
}

// The same filter for a caller who arrives through the gateway email header: no agent authorization, so the id is gw-<userId>.
[TestFixture]
public class GatewayToolUsageTests : McpFixture
{
    private const string Header = "x-user-email";

    protected override bool WithProbeTools => false;

    protected override IReadOnlyDictionary<string, string?> HostSettings { get; } = new Dictionary<string, string?>
    {
        [McpGatewaySettings.EmailHeaderKey] = Header,
    };

    private RecordingUsageEvents Usage => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearUsage() => Usage.Clear();

    [Test]
    public async Task ListProjects_WhenGatewayCaller_ShouldTrackGatewayAgentActor()
    {
        var reply = await CallToolAsync(null, "list_projects", "{}", request => request.Headers.TryAddWithoutValidation(Header, TestUser.Joel.Email));

        reply.IsToolError().ShouldBeFalse();
        var tracked = await Usage.WaitForAsync(e => e.Source == "tool:list_projects", TimeSpan.FromSeconds(10));
        tracked.Actor.Kind.ShouldBe(UsageActorKind.Agent);
        tracked.Actor.UserId.ShouldBe(TestUser.Joel.Id);
        tracked.Actor.AgentId.ShouldBe($"gw-{TestUser.Joel.Id}");
        tracked.Actor.DistinctId.ShouldBe($"agent:gw-{TestUser.Joel.Id}");
        Usage.Events.Where(e => e.Source == "tool:list_projects").ShouldHaveSingleItem();
    }
}
