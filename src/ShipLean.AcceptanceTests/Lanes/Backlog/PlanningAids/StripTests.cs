using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Backlog.Bulk;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Mcp;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.PlanningAids;

// "Before you plan" on the seeded Storefront project (SHIP), signed in as Joel, against the real server's five checks.
// The seed leaves 1 story unestimated and 12 without acceptance criteria; Order(1) and Order(2) fix one row each.
[TestFixture]
public class StripTests : SeededAcceptanceTest
{
    private const string FixedByApi = "SHIP-144";

    [Test]
    [Order(1)]
    public async Task Strip_WhenCountClicked_ShouldWorkThroughRowsAndAgreeWithReviewBacklog()
    {
        var backlog = new StoriesBacklog(Page);
        var strip = new BeforeYouPlanStrip(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(strip.Strip).ToBeVisibleAsync();
        await new LiveStatus(Page).WaitUntilLiveAsync();

        var token = await Host.IssueAgentToken(TestUser.Joel, new HashSet<string> { "shiplean:read" });
        await using var mcp = await McpTestClient.ConnectAsync(new Uri($"{BaseUrl}/mcp"), token);
        var review = await ReviewAsync(mcp);
        var counts = await strip.CountsAsync();

        // The strip and review_backlog read the same checks.
        counts[BeforeYouPlanStrip.Unestimated].ShouldBe(review.Keys("unestimated").Count);
        counts[BeforeYouPlanStrip.NoCriteria].ShouldBe(review.Keys("no_acceptance_criteria").Count);
        counts[BeforeYouPlanStrip.NoParent].ShouldBe(review.Keys("no_parent").Count);
        counts[BeforeYouPlanStrip.Stale].ShouldBe(review.Keys("stale").Count);
        counts[BeforeYouPlanStrip.Duplicates].ShouldBe(review.Keys("likely_duplicate").Count);
        counts[BeforeYouPlanStrip.NoCriteria].ShouldBeGreaterThan(1);
        review.Keys("no_acceptance_criteria").ShouldContain(FixedByApi);

        await strip.Check(BeforeYouPlanStrip.NoCriteria).ClickAsync();

        // The queue is the check's rows, the same ones review_backlog flags.
        var flagged = review.Keys("no_acceptance_criteria");
        await Expect(backlog.Rows).ToHaveCountAsync(flagged.Count);
        (await backlog.RowKeys.AllInnerTextsAsync()).OrderBy(k => k).ShouldBe(flagged.OrderBy(k => k));
        await Expect(strip.QueueBar).ToHaveTextAsync($"0 of {flagged.Count} fixed");
        await Expect(strip.DoneMarks).ToHaveCountAsync(0);

        // Someone writes acceptance criteria on one row: it stays in the queue wearing its done mark.
        await PlanningAidsApi.SetCriteriaAsync(Page.Context.APIRequest, FixedByApi, "Given a saved card, when the customer pays, then the order is placed.");
        var fixedRow = backlog.Rows.Filter(new() { Has = Page.GetByTestId(BacklogTestIds.Key).Filter(new() { HasTextString = FixedByApi }) });
        await Expect(fixedRow.GetByTestId(BacklogTestIds.QueueDone)).ToBeVisibleAsync();
        await Expect(strip.DoneMarks).ToHaveCountAsync(1);
        await Expect(strip.QueueBar).ToHaveTextAsync($"1 of {flagged.Count} fixed");
        await Expect(backlog.Rows).ToHaveCountAsync(flagged.Count);
        await Expect(strip.Check(BeforeYouPlanStrip.NoCriteria)).ToContainTextAsync((flagged.Count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture));

        // review_backlog agrees that the row is fixed.
        (await ReviewAsync(mcp)).Keys("no_acceptance_criteria").ShouldNotContain(FixedByApi);

        await strip.QueueExit.ClickAsync();
        await Expect(strip.QueueBar).ToHaveCountAsync(0);
        await Expect(strip.DoneMarks).ToHaveCountAsync(0);
        (await backlog.Rows.CountAsync()).ShouldBeGreaterThan(flagged.Count);
    }

    [Test]
    [Order(2)]
    public async Task Strip_WhenRowEstimatedByKeys_ShouldKeepItsDoneMarkInQueue()
    {
        var backlog = new StoriesBacklog(Page);
        var strip = new BeforeYouPlanStrip(Page);
        var keys = new BacklogKeys(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(strip.Check(BeforeYouPlanStrip.Unestimated)).ToContainTextAsync("1");

        await strip.Check(BeforeYouPlanStrip.Unestimated).ClickAsync();
        await Expect(backlog.Rows).ToHaveCountAsync(1);
        await Expect(strip.QueueBar).ToHaveTextAsync("0 of 1 fixed");
        var row = backlog.Rows.First;
        await row.FocusAsync();

        // A digit starts the estimate, Enter saves it.
        await keys.PressAsync("5");
        var field = Page.GetByTestId(BacklogTestIds.PointsInput);
        await Expect(field).ToHaveValueAsync("5");
        var saved = await Page.RunAndWaitForResponseAsync(
            () => keys.PressAsync("Enter"),
            r => r.Request.Method == "PATCH" && r.Url.Contains("/api/work-items/", StringComparison.Ordinal));
        saved.Ok.ShouldBeTrue(await saved.TextAsync());

        await Expect(row.GetByTestId(BacklogTestIds.QueueDone)).ToBeVisibleAsync();
        await Expect(backlog.Rows).ToHaveCountAsync(1);
        await Expect(strip.QueueBar).ToHaveTextAsync("1 of 1 fixed");
        await Expect(strip.Check(BeforeYouPlanStrip.Unestimated)).ToContainTextAsync("✓");
    }

    [Test]
    [Order(3)]
    public async Task Strip_WhenEscPressedInQueue_ShouldLeaveIt()
    {
        var backlog = new StoriesBacklog(Page);
        var strip = new BeforeYouPlanStrip(Page);
        var keys = new BacklogKeys(Page);
        await backlog.OpenAsync("SHIP");

        await strip.Check(BeforeYouPlanStrip.Stale).ClickAsync();
        await Expect(strip.QueueBar).ToBeVisibleAsync();
        await backlog.Rows.First.FocusAsync();
        await keys.PressAsync("Escape");

        await Expect(strip.QueueBar).ToHaveCountAsync(0);
        (await backlog.Rows.CountAsync()).ShouldBeGreaterThan(10);
    }

    [Test]
    [Order(4)]
    public async Task Strip_WhenCountsAboveZero_ShouldNotBlockPull()
    {
        var planning = new PlanningBacklog(Page);
        var strip = new BeforeYouPlanStrip(Page);
        await planning.OpenAsync("SHIP");
        var counts = await strip.CountsAsync();
        counts[BeforeYouPlanStrip.NoCriteria].ShouldBeGreaterThan(0);
        counts[BeforeYouPlanStrip.Stale].ShouldBeGreaterThan(0);

        var go = Page.GetByTestId(BacklogTestIds.PullGo);
        await Expect(go).ToBeEnabledAsync();
        var line = Page.GetByTestId(BacklogTestIds.PullLine);
        await Expect(line).ToContainTextAsync("Everything above this line");

        var response = await Page.RunAndWaitForApiAsync($"/api/projects/{SeedIds.Projects.Ship}/backlog/bulk", "POST", () => go.ClickAsync());

        response.Ok.ShouldBeTrue(await response.TextAsync());
        var result = (await response.JsonAsync()).GetValueOrDefault();
        result.GetProperty("written").GetArrayLength().ShouldBeGreaterThan(0);
        result.GetProperty("stale").GetArrayLength().ShouldBe(0);

        // The server holds what the response says: every written row is now in Sprint 25, the sprint the pull line fills.
        var written = result.GetProperty("written").EnumerateArray().Select(w => w.GetProperty("key").GetString()!).ToList();
        foreach (var key in written)
        {
            (await BulkReadBack.ReadAsync(Page.Context.APIRequest, key)).Sprint.ShouldBe("Sprint 25", $"{key} after Pull");
        }
    }

    [Test]
    [Order(5)]
    public async Task Strip_WhenQueueOpenInPlanning_ShouldStopDraggingAndKeepAdding()
    {
        var planning = new PlanningBacklog(Page);
        var strip = new BeforeYouPlanStrip(Page);
        await planning.OpenAsync("SHIP");

        await strip.Check(BeforeYouPlanStrip.NoCriteria).ClickAsync();

        await Expect(Page.GetByTestId(BacklogTestIds.DragOffBanner)).ToBeVisibleAsync();
        await Expect(Page.GetByTestId(BacklogTestIds.Grip).First).ToBeDisabledAsync();
        await Expect(Page.GetByTestId(BacklogTestIds.PullLine)).ToHaveCountAsync(0);
        await Expect(planning.NewItem).ToBeEnabledAsync();
        await planning.NewItem.ClickAsync();
        await Expect(planning.AddRow).ToBeVisibleAsync();
    }

    private static async Task<ReviewResult> ReviewAsync(McpClient mcp)
    {
        var result = await mcp.CallToolAsync("review_backlog", new Dictionary<string, object?> { ["project"] = "SHIP", ["top"] = 100 });
        result.StructuredContent.ShouldNotBeNull();
        result.IsError.ShouldNotBe(true, result.StructuredContent.Value.ToString());
        return new ReviewResult(result.StructuredContent.Value);
    }

    // review_backlog's answer: the keys each flag names, among the top 100 of the stack.
    private sealed record ReviewResult(JsonElement Json)
    {
        public IReadOnlyList<string> Keys(string flag) =>
            [.. Json.GetProperty("items").EnumerateArray()
                .Where(i => i.GetProperty("flags").EnumerateArray().Any(f => f.GetString() == flag))
                .Select(i => i.GetProperty("key").GetString()!)];
    }
}
