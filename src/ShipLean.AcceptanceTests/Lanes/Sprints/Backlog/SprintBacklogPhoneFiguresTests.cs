using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using static ShipLean.AcceptanceTests.Lanes.Sprints.Backlog.BacklogApi;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.Backlog;

// The Sprint backlog's figures and glance rows on a phone (SHIP-724), on seeded Sprint 24 of SHIP. Nothing here changes data.
[TestFixture]
public class SprintBacklogPhoneFiguresTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task SprintBacklog_WhenOpenedOnPhone_ShouldShowFourFigureTilesAboveTheList()
    {
        var backlog = new SprintBacklogPhoneFigures(Page);

        await backlog.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);

        await Expect(backlog.Tiles).ToHaveCountAsync(4);
        await Expect(backlog.PointsBar).ToBeVisibleAsync();
        await Expect(backlog.Glance).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, backlog.Figures)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, backlog.Glance)).ShouldBeEmpty();
        var tiles = await backlog.Tiles.EvaluateAllAsync<double[][]>("els => els.map(e => { const r = e.getBoundingClientRect(); return [r.top, r.bottom, r.left, r.right]; })");
        tiles.Select(t => t[0]).Distinct().Count().ShouldBe(1, "the four tiles share one row");
        var table = await backlog.Table.BoundingBoxAsync();
        tiles.ShouldAllBe(t => t[1] <= table!.Y);
        (await backlog.Glance.BoundingBoxAsync())!.Y.ShouldBeLessThan(table!.Y);
        (await backlog.ScrollsSidewaysAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task SprintBacklog_WhenInProgressNowTappedOnPhone_ShouldListEachMembersActiveItems()
    {
        var sprint = SeedIds.Sprints.Sprint24;
        var expected = await ActiveItemsByUserAsync(sprint);
        expected.ShouldNotBeEmpty();
        var backlog = new SprintBacklogPhoneFigures(Page);
        await backlog.OpenAsync("SHIP", sprint);

        await backlog.ActiveRow.TapAsync();

        await Expect(backlog.Sheet).ToBeVisibleAsync();
        await Expect(backlog.People.First).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, backlog.Sheet)).ShouldBeEmpty();
        var shown = await backlog.SheetItemsAsync();
        foreach (var (user, keys) in expected)
        {
            shown.ShouldContainKey(user);
            shown[user].OrderBy(k => k, StringComparer.Ordinal).ShouldBe(keys.OrderBy(k => k, StringComparer.Ordinal));
        }

        shown.Where(p => !expected.ContainsKey(p.Key)).ShouldAllBe(p => p.Value.Length == 0);
    }

    // The sprint's assigned items in an in-progress state, by assignee, read from the API.
    private async Task<Dictionary<string, string[]>> ActiveItemsByUserAsync(Guid sprint)
    {
        var items = await ReadAsync(Page, $"/api/projects/{SeedIds.Projects.Ship}/work-items");
        return items.EnumerateArray()
            .Where(i => i.GetProperty("sprint").ValueKind == JsonValueKind.Object && i.GetProperty("sprint").GetProperty("id").GetGuid() == sprint)
            .Where(i => i.GetProperty("assignee").ValueKind == JsonValueKind.Object && i.GetProperty("state").GetProperty("category").GetString() == "inProgress")
            .GroupBy(i => i.GetProperty("assignee").GetProperty("id").GetGuid().ToString())
            .ToDictionary(g => g.Key, g => g.Select(i => i.GetProperty("key").GetString()!).ToArray());
    }
}
