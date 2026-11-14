using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog.Touch;

// The row ⋯ and the Points sheet on the Stories backlog's touch rows (SHIP-716 D9).
public sealed class RowActions(IPage page)
{
    public ILocator Sheet => page.GetByTestId(TouchTestIds.Sheet);

    public ILocator PointsSheet => page.GetByTestId(BacklogTestIds.PointsSheet);

    public ILocator PointsInput => page.GetByTestId(BacklogTestIds.PointsSheetInput);

    public ILocator PointsSave => page.GetByTestId(BacklogTestIds.PointsSheetSave);

    // The rows that have no estimate: their points show a ? chip.
    public ILocator ParentSheet => page.GetByTestId(BacklogTestIds.RowParentSheet);

    public ILocator UnestimatedCheck => page.GetByTestId(BacklogTestIds.PlanCheck).Filter(new() { HasTextString = "not estimated" });

    public ILocator QueueBanner => page.GetByTestId(BacklogTestIds.QueueBanner);

    public ILocator QueueStepKey => page.GetByTestId(BacklogTestIds.QueueStepKey);

    public ILocator QueueSkip => page.GetByTestId(BacklogTestIds.QueueSkip);

    // Adds stories with no estimate at the top of the backlog (K2), so the queue has rows of this test's own.
    public static async Task AddUnestimatedStoriesAsync(IAPIRequestContext request, Guid projectId, int count)
    {
        var items = Enumerable.Range(1, count).Select(n => new
        {
            type = "story",
            title = $"Queue story {n:000}",
            description = (string?)null,
            points = (int?)null,
            parentId = (Guid?)null,
            tasks = Array.Empty<object>(),
        }).ToList();
        var response = await request.PostAsync($"/api/projects/{projectId}/backlog/items", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-ShipLean-Client"] = "row-actions-test" },
            DataObject = new { items, sprintId = (Guid?)null, placement = (object?)null },
        });
        response.Ok.ShouldBeTrue(await response.TextAsync());
    }

    public ILocator UnestimatedRows => page.GetByTestId(BacklogTestIds.Row)
        .Filter(new() { Has = page.GetByTestId(BacklogTestIds.Points).Filter(new() { HasTextString = "?" }) });

    public ILocator Row(string itemKey) => page.GetByTestId(BacklogTestIds.Row).Filter(new() { Has = page.GetByTestId(BacklogTestIds.Key).Filter(new() { HasTextRegex = new System.Text.RegularExpressions.Regex($"^{itemKey}$") }) });

    public async Task OpenMoreAsync(ILocator row)
    {
        await row.GetByTestId(TouchTestIds.MoreButton).TapAsync();
        await Assertions.Expect(Sheet).ToBeVisibleAsync();
    }

    public async Task PickAsync(string actionLabel) =>
        await Sheet.GetByTestId(TouchTestIds.ActionSheetRow).Filter(new() { HasTextString = actionLabel }).TapAsync();
}
