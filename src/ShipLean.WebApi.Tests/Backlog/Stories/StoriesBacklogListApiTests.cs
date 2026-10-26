using System.Net;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backlog.Stories;

// List mode and the query parameters, over the seeded SHIP backlog.
[TestFixture]
public class StoriesBacklogListApiTests : ApiFixture
{
    private static readonly string Url = $"api/projects/{SeedIds.Projects.Ship}/backlog";

    private const string PaymentsFilter = """{"match":"all","clauses":[{"field":"tags","op":"contains","value":"Payments"}]}""";

    [Test]
    public async Task Backlog_WhenList_ShouldBeInStackOrderWithSprintChipsAndTags()
    {
        var view = await Api.GetBacklogAsync($"{Url}?mode=list");

        view.Mode.ShouldBe(BacklogMode.List);
        view.Sections.ShouldBeEmpty();
        view.TotalCount.ShouldBe(view.Rows.Count);
        view.Rows.ShouldAllBe(row => row.Kind == BacklogRowKind.Item && row.Depth == 0);
        view.Rows.Select(row => row.Rank).ShouldBe(Enumerable.Range(1, view.Rows.Count).Select(n => (int?)n));
        view.Rows.Select(row => row.Key).Take(5).ShouldBe(["SHIP-141", "SHIP-144", "SHIP-129", "SHIP-151", "SHIP-153"]);
        view.Rows[0].Sprint.ShouldNotBeNull().Name.ShouldBe("Sprint 24");
        view.Rows.Single(row => row.Key == "SHIP-156").Sprint.ShouldBeNull();
        view.Rows.Single(row => row.Key == "SHIP-129").Tags.ShouldBe([new CardTagDto("Mobile", Swatch.Info), new CardTagDto("Blocked", Swatch.Danger)]);
        view.TagColours.ShouldContain(new TagColourJson("Payments", Swatch.Accent));
    }

    [Test]
    public async Task Backlog_WhenCompletedHidden_ShouldOmitCompletedCategory()
    {
        var hidden = await Api.GetBacklogAsync($"{Url}?mode=list");
        var shown = await Api.GetBacklogAsync($"{Url}?mode=list&includeCompleted=true");

        hidden.Rows.ShouldNotContain(row => row.State!.Category == StateCategory.Completed);
        shown.Rows.ShouldContain(row => row.Key == "SHIP-138");
        shown.Rows.Count(row => row.State!.Category == StateCategory.Completed).ShouldBeGreaterThan(0);
        shown.TotalCount.ShouldBeGreaterThan(hidden.TotalCount);
    }

    [Test]
    public async Task Backlog_WhenFiltered_ShouldReturnOnlyMatches()
    {
        var view = await Api.GetBacklogAsync($"{Url}?mode=list&filter={Uri.EscapeDataString(PaymentsFilter)}");

        view.Rows.Select(row => row.Key).ShouldBe(["SHIP-141", "SHIP-158"]);
        view.TotalCount.ShouldBe(2);
    }

    [Test]
    public async Task Backlog_WhenFiltered_ShouldKeepFullBacklogRank()
    {
        var all = await Api.GetBacklogAsync($"{Url}?mode=list");
        var filtered = await Api.GetBacklogAsync($"{Url}?mode=list&filter={Uri.EscapeDataString(PaymentsFilter)}");

        var fullRank = all.Rows.Single(row => row.Key == "SHIP-158").Rank;
        fullRank.ShouldBe(8);
        filtered.Rows.Single(row => row.Key == "SHIP-158").Rank.ShouldBe(fullRank);
        filtered.Rows.ShouldAllBe(row => row.Rank == all.Rows.Single(a => a.Key == row.Key).Rank);
    }

    [TestCase("not json")]
    [TestCase("""{"match":"all","clauses":[null]}""")]
    [TestCase("""{"match":"all","clauses":[{"field":"nope","op":"=","value":"x"}]}""")]
    public async Task Backlog_WhenFilterInvalid_ShouldBeValidation(string filter)
    {
        using var response = await Api.GetAsync($"{Url}?mode=list&filter={Uri.EscapeDataString(filter)}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }
}
