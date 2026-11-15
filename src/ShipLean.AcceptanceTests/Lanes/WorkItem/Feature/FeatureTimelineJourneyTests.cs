using Dapper;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.Timeline;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Retros;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Feature;

// A delivered Feature arranged through the database at chosen past times: wave 1 has one task (blocked for an hour, reviewed
// twice), wave 2 has two. The journey: the Feature page's Timeline section shows four key dates, Open timeline shows one bar per
// task, People grouping and back, a selected bar shows its history and reviews in the rail, and JSON shows critical_path.
// It asserts counts, keys and server-made labels, never clock times.
[TestFixture]
public class FeatureTimelineJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private static readonly DateTimeOffset T0 = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task FeatureTimeline_WhenOpenedFromFeature_ShouldShowBarsAndSelectedHistory()
    {
        var (feature, a1, b1, b2) = await ArrangeDeliveredFeatureAsync();
        var timeline = new FeatureTimelinePage(Page);
        await Page.GotoAsync(Links.WorkItem(Project, feature));
        await new LiveStatus(Page).WaitUntilLiveAsync();

        await Expect(FeatureTimelinePage.KeyDatePoints(timeline.Section)).ToHaveCountAsync(4);
        await timeline.OpenFromFeatureAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"{Links.WorkItem(Project, feature)}/timeline"));
        await Expect(timeline.Page).ToBeVisibleAsync();
        await Expect(FeatureTimelinePage.KeyDatePoints(timeline.Page)).ToHaveCountAsync(4);
        await Expect(timeline.Bars).ToHaveCountAsync(3);
        foreach (var key in new[] { a1, b1, b2 })
        {
            await Expect(timeline.Bar(key)).ToHaveCountAsync(1);
        }

        await timeline.GroupButton("people").ClickAsync();
        await Expect(timeline.Toolbar).ToHaveAttributeAsync("data-group", "people");
        await Expect(timeline.Bars).ToHaveCountAsync(3);
        await timeline.GroupButton("stories").ClickAsync();
        await Expect(timeline.Toolbar).ToHaveAttributeAsync("data-group", "stories");
        await Expect(timeline.Bars).ToHaveCountAsync(3);

        await timeline.Bar(a1).ClickAsync();
        await Expect(timeline.RailSelected).ToBeVisibleAsync();
        await Expect(timeline.Rail).ToHaveAttributeAsync("data-selected", a1);
        await Expect(timeline.RailSelected).ToContainTextAsync(a1);
        await Expect(timeline.HistoryRows).ToHaveCountAsync(6);
        await Expect(timeline.ReviewRows).ToHaveCountAsync(2);

        await timeline.ViewButton("json").ClickAsync();
        await Expect(timeline.Toolbar).ToHaveAttributeAsync("data-view", "json");
        await Expect(timeline.Json).ToContainTextAsync("critical_path");
    }

    private async Task<(string Feature, string A1, string B1, string B2)> ArrangeDeliveredFeatureAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var feature = await ItemAsync(connection, "feature", null, T0.AddDays(-2), []);
        var storyA = await ItemAsync(connection, "story", feature.Id, T0.AddDays(-1), ["wave:1"]);
        var storyB = await ItemAsync(connection, "story", feature.Id, T0.AddDays(-1), ["wave:2"]);
        var a1 = await ItemAsync(connection, "task", storyA.Id, T0.AddDays(-1), []);
        var b1 = await ItemAsync(connection, "task", storyB.Id, T0.AddDays(-1), []);
        var b2 = await ItemAsync(connection, "task", storyB.Id, T0.AddDays(-1), []);

        var active = SeedIds.States.Ship.TaskActive;
        var review = SeedIds.States.Ship.TaskInReview;
        await MoveAsync(connection, a1.Id, 2, T0.AddHours(1), active);
        await TagChangeAsync(connection, a1.Id, 3, T0.AddHours(2), "[]", """["blocked"]""");
        await TagChangeAsync(connection, a1.Id, 4, T0.AddHours(3), """["blocked"]""", "[]");
        await MoveAsync(connection, a1.Id, 5, T0.AddHours(4), review);
        await MoveAsync(connection, a1.Id, 6, T0.AddHours(5), active);
        await MoveAsync(connection, a1.Id, 7, T0.AddHours(6), review);
        await ReviewAsync(connection, a1.Id, T0.AddHours(4), "changesRequested");
        await ReviewAsync(connection, a1.Id, T0.AddHours(6), "approved");
        await MoveAsync(connection, b1.Id, 2, T0.AddHours(20), active);
        await MoveAsync(connection, b1.Id, 3, T0.AddHours(30), review);
        await MoveAsync(connection, b2.Id, 2, T0.AddHours(24), active);
        await MoveAsync(connection, b2.Id, 3, T0.AddHours(40), review);
        await connection.ExecuteAsync(
            "INSERT INTO work_item_link (id, source_id, target_id, link_type) VALUES (@id, @source, @target, 'successor')",
            new { id = Guid.NewGuid(), source = a1.Id, target = b1.Id });
        await RetroHistory.CompleteAsync(connection, feature.Id, T0.AddHours(50));
        return (feature.Key, a1.Key, b1.Key, b2.Key);
    }

    private static async Task<(Guid Id, string Key)> ItemAsync(NpgsqlConnection connection, string type, Guid? parent, DateTimeOffset createdAt, string[] tags)
    {
        var builder = new WorkItemBuilder(SeedIds.Projects.Ship).WithType(type).WithCreatedAt(createdAt).WithCreatedBy(SeedIds.Users.Joel).WithTags(tags);
        if (parent is { } parentId)
        {
            builder.WithParent(parentId);
        }
        var id = await builder.InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE revision SET changed_at = @at WHERE work_item_id = @id AND rev = 1", new { id, at = createdAt });
        return (id, await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }) ?? throw new InvalidOperationException());
    }

    private static async Task MoveAsync(NpgsqlConnection connection, Guid id, int rev, DateTimeOffset at, Guid state)
    {
        await new RevisionBuilder(id).WithRev(rev).WithChangedAt(at).WithChangedBy(SeedIds.Users.Joel)
            .WithChanges($$"""[{"field":"state","from":null,"to":"{{state}}"}]""").InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @state, revision = @rev WHERE id = @id", new { id, state, rev });
    }

    private static async Task TagChangeAsync(NpgsqlConnection connection, Guid id, int rev, DateTimeOffset at, string from, string to)
        => await new RevisionBuilder(id).WithRev(rev).WithChangedAt(at).WithChangedBy(SeedIds.Users.Joel)
            .WithChanges($$"""[{"field":"tags","from":{{from}},"to":{{to}}}]""").InsertAsync(connection);

    private static async Task ReviewAsync(NpgsqlConnection connection, Guid id, DateTimeOffset at, string verdict)
        => await connection.ExecuteAsync(
            "INSERT INTO review_event (id, work_item_id, verdict, note, by_user_id, agent_name, at) VALUES (@eventId, @id, @verdict, NULL, @by, 'reviewer', @at)",
            new { eventId = Guid.NewGuid(), id, verdict, by = SeedIds.Users.Joel, at });
}
