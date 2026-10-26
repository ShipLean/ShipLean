using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backlog.Stories;

// Tree mode. SHIP-170 is made parentless so the "No parent" group has a member; only this class does that.
[TestFixture]
public class StoriesBacklogTreeApiTests : ApiFixture
{
    private static readonly string Url = $"api/projects/{SeedIds.Projects.Ship}/backlog?mode=tree";

    private const string PaymentsFilter = """{"match":"all","clauses":[{"field":"tags","op":"contains","value":"Payments"}]}""";

    [OneTimeSetUp]
    public async Task MakeStoryParentless()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE work_item SET parent_id = NULL, parent_type = NULL WHERE key = 'SHIP-170'";
        await command.ExecuteNonQueryAsync();
    }

    [Test]
    public async Task Backlog_WhenTree_ShouldGroupUnderEpicFeature()
    {
        var view = await Api.GetBacklogAsync(Url);

        var checkout = view.Rows.Select((row, index) => (row, index)).Single(x => x.row.Key == "SHIP-90");
        checkout.row.Kind.ShouldBe(BacklogRowKind.Group);
        checkout.row.Depth.ShouldBe(0);
        checkout.row.Rank.ShouldBeNull();
        var feature = view.Rows[checkout.index + 1];
        (feature.Key, feature.Kind, feature.Depth).ShouldBe(("SHIP-101", BacklogRowKind.Group, 1));
        var story = view.Rows[checkout.index + 2];
        (story.Key, story.Kind, story.Depth).ShouldBe(("SHIP-141", BacklogRowKind.Item, 2));
        story.Rank.ShouldNotBeNull();
    }

    [Test]
    public async Task Backlog_WhenNestedFeature_ShouldIndentUnderItsFeature()
    {
        var view = await Api.GetBacklogAsync(Url);

        var depths = view.Rows.ToDictionary(row => row.Key, row => row.Depth);
        (depths["SHIP-92"], depths["SHIP-103"], depths["SHIP-108"], depths["SHIP-156"]).ShouldBe((0, 1, 2, 3));
        var order = view.Rows.Select(row => row.Key).ToList();
        order.IndexOf("SHIP-108").ShouldBeGreaterThan(order.IndexOf("SHIP-103"));
    }

    [Test]
    public async Task Backlog_WhenTree_ShouldPutNoParentGroupLast()
    {
        var view = await Api.GetBacklogAsync(Url);

        var last = view.Rows[^2];
        (last.Kind, last.Id, last.Key, last.Title, last.SumWarn).ShouldBe((BacklogRowKind.NoParentGroup, Guid.Empty, string.Empty, "No parent", false));
        last.Type.ShouldBeNull();
        last.Rollups.ShouldBe([new RollupCountDto(WorkItemTypeId.Story, 1, 17), new RollupCountDto(WorkItemTypeId.Bug, 0, 5)]);
        last.ChildPoints.ShouldBe(95m);
        var member = view.Rows[^1];
        (member.Key, member.Depth, member.Kind).ShouldBe(("SHIP-170", 1, BacklogRowKind.Item));
    }

    [Test]
    public async Task Backlog_WhenTree_ShouldSumOpenAndClosedPointsAndWarnAsTheFeaturesBacklogDoes()
    {
        var view = await Api.GetBacklogAsync(Url);
        var features = await Api.GetBacklogAsync($"api/projects/{SeedIds.Projects.Ship}/backlogs/features");

        // SHIP-101 has 20 points of its own and 26 under it, closed stories included; SHIP-91's 32 under 30 stays calm.
        var warned = view.Rows.Single(row => row.Key == "SHIP-101" && row.Kind == BacklogRowKind.Group);
        (warned.ChildPoints, warned.SumWarn).ShouldBe((26m, true));
        foreach (var feature in features.Rows)
        {
            var group = view.Rows.Single(row => row.Key == feature.Key && row.Kind == BacklogRowKind.Group);
            (group.ChildPoints, group.SumWarn).ShouldBe((feature.ChildPoints, feature.SumWarn), feature.Key);
        }
    }

    [Test]
    public async Task Backlog_WhenFiltered_ShouldCountOnlyFilteredChildrenInGroups()
    {
        var all = await Api.GetBacklogAsync(Url);
        var filtered = await Api.GetBacklogAsync($"{Url}&filter={Uri.EscapeDataString(PaymentsFilter)}");

        var allStories = all.Rows.Single(row => row.Key == "SHIP-101").Rollups.Single(r => r.Type == WorkItemTypeId.Story);
        var payments = filtered.Rows.Single(row => row.Key == "SHIP-101");
        payments.Rollups.Single(r => r.Type == WorkItemTypeId.Story).Open.ShouldBeLessThan(allStories.Open);
        filtered.Rows.Where(row => row.Kind == BacklogRowKind.Item).Select(row => row.Key).ShouldBe(["SHIP-141", "SHIP-158"]);
        filtered.Rows.ShouldNotContain(row => row.Key == "SHIP-92");
        filtered.Rows.ShouldNotContain(row => row.Kind == BacklogRowKind.NoParentGroup);
    }

    [Test]
    public async Task Backlog_WhenTreeFiltered_ShouldKeepFullBacklogRank()
    {
        var all = await Api.GetBacklogAsync(Url);
        var filtered = await Api.GetBacklogAsync($"{Url}&filter={Uri.EscapeDataString(PaymentsFilter)}");

        var full = all.Rows.Where(row => row.Kind == BacklogRowKind.Item).ToDictionary(row => row.Key, row => row.Rank);
        filtered.Rows.Where(row => row.Kind == BacklogRowKind.Item).ShouldAllBe(row => row.Rank == full[row.Key]);
    }

    [Test]
    public async Task Backlog_WhenTree_ShouldLeaveTasksOutOfGroupRollups()
    {
        var view = await Api.GetBacklogAsync(Url);

        view.Rows.Where(row => row.Kind != BacklogRowKind.Item).SelectMany(row => row.Rollups).ShouldNotContain(r => r.Type == WorkItemTypeId.Task);
        view.Rows.Where(row => row.Kind == BacklogRowKind.Group).ShouldAllBe(row => row.Rollups.Count > 0);
    }

    [Test]
    public async Task Backlog_WhenCompletedHiddenInTree_ShouldStillCountClosedInGroups()
    {
        var hidden = await Api.GetBacklogAsync(Url);
        var shown = await Api.GetBacklogAsync($"{Url}&includeCompleted=true");

        var groups = hidden.Rows.Where(row => row.Kind != BacklogRowKind.Item).Select(row => (row.Key, row.Rollups)).ToList();
        groups.Select(g => g.Key).ShouldBe(shown.Rows.Where(row => row.Kind != BacklogRowKind.Item && groups.Any(g => g.Key == row.Key)).Select(row => row.Key));
        foreach (var (key, rollups) in groups)
        {
            shown.Rows.First(row => row.Kind != BacklogRowKind.Item && row.Key == key).Rollups.ShouldBe(rollups);
        }
    }
}
