using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Timeline;

// feature_timeline over a double that returns the pinned running fixture: not-started items carry assignee in MCP naming.
[TestFixture]
public class FeatureTimelineToolTestsRunning : McpFixture
{
    private string _featureKey = string.Empty;

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices?.Invoke(services);
        services.AddScoped<IFeatureTimeline, RunningFixtureFeatureTimeline>();
    };

    [OneTimeSetUp]
    public async Task ArrangeFeature()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Running timeline feature").InsertAsync(connection);
        _featureKey = await TimelineTestKeys.KeyAsync(connection, feature);
    }

    [Test]
    public async Task FeatureTimeline_WhenNotStartedItems_ShouldSendEachAssigneeOrNull()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "feature_timeline", $$"""{"key":"{{_featureKey}}"}""");

        reply.IsToolError().ShouldBeFalse();
        var body = JsonNode.Parse(reply.Structured().GetRawText())!.AsObject();
        var notStarted = body["not_started"]!.AsArray();
        notStarted.ShouldNotBeEmpty();
        notStarted.ShouldAllBe(n => n!.AsObject().ContainsKey("assignee"));
        notStarted.ShouldContain(n => n!["assignee"] == null);
        var assigned = notStarted.First(n => n!["assignee"] != null)!["assignee"]!.AsObject();
        assigned["name"]!.GetValue<string>().ShouldBe("Ana Costa");
        assigned["agent"]!.GetValue<bool>().ShouldBeFalse();
        assigned.ContainsKey("user_id").ShouldBeTrue();
        body["as_of_label"]!.GetValue<string>().ShouldBe("Sat 24 Oct, 14:00");
    }
}
