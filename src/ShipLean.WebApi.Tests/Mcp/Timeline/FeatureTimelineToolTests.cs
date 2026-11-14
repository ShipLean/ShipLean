using System.Text.Json;
using Dapper;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Timeline;

// feature_timeline over a double that returns the pinned fixture: the tool's JSON is the MCP fixture plus project and open_url.
[TestFixture]
public class FeatureTimelineToolTests : McpFixture
{
    private string _featureKey = string.Empty;

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices?.Invoke(services);
        services.AddScoped<IFeatureTimeline, FixtureFeatureTimeline>();
    };

    [OneTimeSetUp]
    public async Task ArrangeFeature()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Timeline feature").InsertAsync(connection);
        _featureKey = await TimelineTestKeys.KeyAsync(connection, feature);
    }

    [Test]
    public async Task FeatureTimeline_WhenFeature_ShouldReturnTheReadsModelInMcpNaming()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "feature_timeline", $$"""{"key":"{{_featureKey}}"}""");

        reply.IsToolError().ShouldBeFalse();
        var body = JsonNode.Parse(reply.Structured().GetRawText())!.AsObject();
        body["project"]!.GetValue<string>().ShouldBe("SHIP");
        body["open_url"]!.GetValue<string>().ShouldEndWith($"/p/SHIP/items/{_featureKey}");
        body.Remove("project");
        body.Remove("open_url");
        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "views", "feature-timeline-mcp.json")))!;
        JsonNode.DeepEquals(body, expected).ShouldBeTrue(body.ToJsonString());
    }

    [Test]
    public async Task FeatureTimeline_WhenFeature_ShouldSummariseTasksDaysAndState()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "feature_timeline", $$"""{"key":"{{_featureKey}}"}""");

        var summary = reply.ResultText();
        summary.ShouldStartWith($"{_featureKey} timeline: {FixtureFeatureTimeline.View.Bars.Count} tasks over ");
        summary.ShouldEndWith(", finished");
    }
}

// The same tool over the real read: only a Feature has a timeline.
[TestFixture]
public class FeatureTimelineToolTestsRealRead : McpFixture
{
    private string _storyKey = string.Empty;

    protected override bool WithProbeTools => false;

    [OneTimeSetUp]
    public async Task ArrangeStory()
    {
        await using var connection = await OpenDatabaseAsync();
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("A story").InsertAsync(connection);
        _storyKey = await TimelineTestKeys.KeyAsync(connection, story);
    }

    [Test]
    public async Task FeatureTimeline_WhenStoryKey_ShouldReturnNotFound()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "feature_timeline", $$"""{"key":"{{_storyKey}}"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
    }
}

internal static class TimelineTestKeys
{
    public static Task<string> KeyAsync(Npgsql.NpgsqlConnection connection, Guid id)
        => connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });
}
