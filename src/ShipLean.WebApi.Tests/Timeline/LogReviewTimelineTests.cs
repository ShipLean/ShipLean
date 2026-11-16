using Dapper;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Timeline;

// The review chain end to end: log_review writes the rounds and feature_timeline draws them on the task's bar. log_review stamps
// the host's clock, so the task's moves are placed a few hours before now and every assertion is relative (order, after the bar).
[TestFixture]
public class LogReviewTimelineTests : McpFixture
{
    private static readonly Guid Ship = SeedIds.Projects.Ship;

    protected override bool WithProbeTools => false;

    [Test]
    public async Task LogReview_ThenFeatureTimeline_ShouldDrawEachRoundAfterTheBar()
    {
        var now = TimeProvider.System.GetUtcNow();
        var feature = await ItemAsync("feature", null, now.AddHours(-5));
        var story = await ItemAsync("story", feature.Id, now.AddHours(-4));
        var task = await ItemAsync("task", story.Id, now.AddHours(-4));
        await MoveAsync(task.Id, 2, now.AddHours(-3), SeedIds.States.Ship.TaskActive);
        await MoveAsync(task.Id, 3, now.AddHours(-2), SeedIds.States.Ship.TaskInReview);
        var writer = await TokenAsync(TestUser.Joel, ReadWrite);

        var first = await CallToolAsync(writer, "log_review", $$"""{"item":"{{task.Key}}","verdict":"changes_requested","note":"Missing a test."}""");
        var second = await CallToolAsync(writer, "log_review", $$"""{"item":"{{task.Key}}","verdict":"approved"}""");
        var timeline = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "feature_timeline", $$"""{"key":"{{feature.Key}}"}""");

        first.IsToolError().ShouldBeFalse(first.ResultText());
        second.IsToolError().ShouldBeFalse(second.ResultText());
        timeline.IsToolError().ShouldBeFalse(timeline.ResultText());
        var bar = timeline.Structured().GetProperty("bars").EnumerateArray().Single(b => b.GetProperty("key").GetString() == task.Key);
        var reviews = bar.GetProperty("reviews").EnumerateArray().ToList();
        reviews.Select(r => r.GetProperty("verdict").GetString()).ShouldBe(["changes_requested", "approved"]);
        reviews[0].GetProperty("note").GetString().ShouldBe("Missing a test.");
        reviews.ShouldAllBe(r => r.GetProperty("by").GetProperty("agent").GetBoolean());
        var end = bar.GetProperty("end_hours").GetDouble();
        reviews.ShouldAllBe(r => r.GetProperty("at_hours").GetDouble() >= end);
    }

    private async Task<(Guid Id, string Key)> ItemAsync(string type, Guid? parent, DateTimeOffset createdAt)
    {
        var builder = new WorkItemBuilder(Ship).WithType(type).WithCreatedAt(createdAt).WithCreatedBy(SeedIds.Users.Joel);
        if (parent is { } parentId)
        {
            builder.WithParent(parentId);
        }
        await using var connection = await OpenDatabaseAsync();
        var id = await builder.InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE revision SET changed_at = @at WHERE work_item_id = @id AND rev = 1", new { id, at = createdAt });
        return (id, await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }) ?? throw new InvalidOperationException());
    }

    private async Task MoveAsync(Guid id, int rev, DateTimeOffset at, Guid state)
    {
        await using var connection = await OpenDatabaseAsync();
        await new RevisionBuilder(id).WithRev(rev).WithChangedAt(at).WithChangedBy(SeedIds.Users.Joel)
            .WithChanges($$"""[{"field":"state","from":null,"to":"{{state}}"}]""").InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @state, revision = @rev WHERE id = @id", new { id, state, rev });
    }
}
