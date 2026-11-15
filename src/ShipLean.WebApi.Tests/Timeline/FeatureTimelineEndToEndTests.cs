using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Dapper;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Timeline;

// The real facts reader and both builder halves on one delivered plan, through the endpoint and through feature_timeline. Two
// waves, three tasks: A1 (wave 1) is blocked for an hour and reviewed twice; B1 follows A1 by a link; B2 follows A1 by the wave
// order. Every time is chosen (SQL at fixed instants), so the facts are the same on every run. One scenario per class.
[TestFixture]
public class FeatureTimelineEndToEndTests : McpFixture
{
    private static readonly Guid Ship = SeedIds.Projects.Ship;
    private static readonly DateTimeOffset T0 = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private (Guid Id, string Key) _feature;
    private string _a1 = string.Empty;
    private string _b1 = string.Empty;
    private string _b2 = string.Empty;

    protected override TestUser SignedInAs => TestUser.Joel;

    protected override bool WithProbeTools => false;

    [OneTimeSetUp]
    public async Task ArrangeDeliveredPlan()
    {
        _feature = await ItemAsync("feature", null, T0.AddDays(-2), []);
        var storyA = await ItemAsync("story", _feature.Id, T0.AddDays(-1), ["wave:1"]);
        var storyB = await ItemAsync("story", _feature.Id, T0.AddDays(-1), ["wave:2"]);
        var a1 = await ItemAsync("task", storyA.Id, T0.AddDays(-1), []);
        var b1 = await ItemAsync("task", storyB.Id, T0.AddDays(-1), []);
        var b2 = await ItemAsync("task", storyB.Id, T0.AddDays(-1), []);
        (_a1, _b1, _b2) = (a1.Key, b1.Key, b2.Key);

        var active = SeedIds.States.Ship.TaskActive;
        var review = SeedIds.States.Ship.TaskInReview;
        await MoveAsync(a1.Id, 2, T0.AddHours(1), active);
        await TagChangeAsync(a1.Id, 3, T0.AddHours(2), "[]", """["blocked"]""");
        await TagChangeAsync(a1.Id, 4, T0.AddHours(3), """["blocked"]""", "[]");
        await MoveAsync(a1.Id, 5, T0.AddHours(4), review);
        await MoveAsync(a1.Id, 6, T0.AddHours(5), active);
        await MoveAsync(a1.Id, 7, T0.AddHours(6), review);
        await ReviewAsync(a1.Id, T0.AddHours(4), "changesRequested");
        await ReviewAsync(a1.Id, T0.AddHours(6), "approved");

        await MoveAsync(b1.Id, 2, T0.AddHours(20), active);
        await MoveAsync(b1.Id, 3, T0.AddHours(30), review);
        await MoveAsync(b2.Id, 2, T0.AddHours(24), active);
        await MoveAsync(b2.Id, 3, T0.AddHours(40), review);
        await LinkAsync(a1.Id, b1.Id);

        await using var connection = await OpenDatabaseAsync();
        await RetroHistory.CompleteAsync(connection, _feature.Id, T0.AddHours(50));
    }

    [Test]
    public async Task Get_WhenPlanDelivered_ShouldDrawItFromHistory()
    {
        var view = await GetViewAsync();

        view.Finished.ShouldBeTrue();
        view.Bars.Select(bar => bar.Key).ShouldBe([_a1, _b1, _b2], ignoreOrder: true);
        var a1 = view.Bars.Single(bar => bar.Key == _a1);
        a1.Start.ShouldBe(T0.AddHours(1));
        a1.End.ShouldBe(T0.AddHours(6));
        a1.State.ShouldBe(TimelineBarState.Resolved);
        a1.History.Select(period => period.State).ShouldBe(
            [TimelinePeriodState.Active, TimelinePeriodState.Blocked, TimelinePeriodState.Active, TimelinePeriodState.Resolved, TimelinePeriodState.Active, TimelinePeriodState.Resolved]);
        a1.History.Single(period => period.State == TimelinePeriodState.Blocked).From.ShouldBe(T0.AddHours(2));
        a1.History.Single(period => period.State == TimelinePeriodState.Blocked).To.ShouldBe(T0.AddHours(3));
        a1.Reviews.Select(review => (review.At, review.Verdict)).ShouldBe(
            [(T0.AddHours(4), ReviewVerdict.ChangesRequested), (T0.AddHours(6), ReviewVerdict.Approved)]);
        view.Bars.Single(bar => bar.Key == _b1).Start.ShouldBe(T0.AddHours(20));
        view.Bars.Single(bar => bar.Key == _b2).End.ShouldBe(T0.AddHours(40));

        var link = view.Edges.Single(edge => edge.To == _b1);
        (link.From, link.Kind, link.Source, link.WaitHours, link.WaitLabel).ShouldBe((_a1, TimelineEdgeKind.Depends, TimelineEdgeSource.Link, 14d, "14 h"));
        var wave = view.Edges.Single(edge => edge.To == _b2);
        (wave.From, wave.Kind, wave.Source, wave.WaitHours, wave.WaitLabel).ShouldBe((_a1, TimelineEdgeKind.Depends, TimelineEdgeSource.Wave, 18d, "18 h"));

        view.CriticalPath.ShouldBe([_a1, _b2]);
        view.Bars.Where(bar => bar.OnCriticalPath).Select(bar => bar.Key).ShouldBe([_a1, _b2], ignoreOrder: true);
        view.Bars.Single(bar => bar.Key == _b1).OnCriticalPath.ShouldBeFalse();

        view.KeyDates.Select(date => date.Point).ShouldBe([KeyDatePoint.Added, KeyDatePoint.Planned, KeyDatePoint.ExecutionStarted, KeyDatePoint.Finished]);
        view.KeyDates.Select(date => date.At).ShouldBe([T0.AddDays(-2), T0.AddDays(-1), T0.AddHours(1), T0.AddHours(50)]);
        view.Gaps.Count.ShouldBe(3);
        view.Gaps[0].Days.ShouldBe(1d);
        view.Gaps.ShouldAllBe(gap => gap.Label != null && !gap.SoFar);
    }

    [Test]
    public async Task FeatureTimeline_McpAndHttp_ShouldMatch()
    {
        var http = await GetViewAsync();
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "feature_timeline", $$"""{"key":"{{_feature.Key}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var mcp = JsonNode.Parse(reply.Structured().GetRawText())!.AsObject();
        mcp["project"]!.GetValue<string>().ShouldBe("SHIP");
        mcp["open_url"]!.GetValue<string>().ShouldEndWith($"/p/SHIP/items/{_feature.Key}");
        mcp.Remove("project");
        mcp.Remove("open_url");
        var expected = JsonSerializer.SerializeToNode(http, TimelineJson.McpOptions);
        JsonNode.DeepEquals(mcp, expected).ShouldBeTrue(mcp.ToJsonString());
        mcp["critical_path"]!.AsArray().Select(node => node!.GetValue<string>()).ShouldBe([_a1, _b2]);
    }

    private async Task<FeatureTimelineViewDto> GetViewAsync()
    {
        using var response = await Api.GetAsync($"api/projects/{Ship}/work-items/by-key/{_feature.Key}/timeline");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<FeatureTimelineViewDto>(text, Wire)!;
    }

    private async Task<(Guid Id, string Key)> ItemAsync(string type, Guid? parent, DateTimeOffset createdAt, string[] tags)
    {
        var builder = new WorkItemBuilder(Ship).WithType(type).WithCreatedAt(createdAt).WithCreatedBy(SeedIds.Users.Joel).WithTags(tags);
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

    private async Task TagChangeAsync(Guid id, int rev, DateTimeOffset at, string from, string to)
    {
        await using var connection = await OpenDatabaseAsync();
        await new RevisionBuilder(id).WithRev(rev).WithChangedAt(at).WithChangedBy(SeedIds.Users.Joel)
            .WithChanges($$"""[{"field":"tags","from":{{from}},"to":{{to}}}]""").InsertAsync(connection);
    }

    private async Task LinkAsync(Guid source, Guid target)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO work_item_link (id, source_id, target_id, link_type) VALUES (@id, @source, @target, 'successor')",
            new { id = Guid.NewGuid(), source, target });
    }

    private async Task ReviewAsync(Guid id, DateTimeOffset at, string verdict)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO review_event (id, work_item_id, verdict, note, by_user_id, agent_name, at) VALUES (@eventId, @id, @verdict, NULL, @by, 'reviewer', @at)",
            new { eventId = Guid.NewGuid(), id, verdict, by = SeedIds.Users.Joel, at });
    }
}
