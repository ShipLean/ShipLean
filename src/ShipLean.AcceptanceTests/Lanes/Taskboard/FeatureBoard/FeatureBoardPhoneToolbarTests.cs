using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Taskboard;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Taskboard.FeatureBoard;

// Checkout v2 (SHIP-101) on a phone (SHIP-722): the header without its back link, and the one-row toolbar with its
// Filter sheet and + Add. Signed in as Joel on the seeded Storefront project.
[TestFixture]
public class FeatureBoardPhoneToolbarTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private const string Feature = "SHIP-101";
    private const string NewStory = "Saved addresses on phone";

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    [Order(1)]
    public async Task FeatureBoard_WhenFilteredByPersonOnPhone_ShouldCountThemAndShowOnlyTheirTasks()
    {
        var grid = new PhoneGridPart(Page);
        var toolbar = new PhoneToolbarPart(Page);
        var (name, count) = await FirstAssignedPersonAsync();

        await Page.GotoAsync(Links.FeatureBoard(Project, Feature));
        await grid.WaitAsync();

        await Expect(Page.GetByTestId(FeatureBoardTestIds.Back)).ToBeHiddenAsync();
        await Expect(toolbar.FilterButton).ToHaveTextAsync("Filter");
        (await TouchRules.ViolationsAsync(Page, toolbar.Toolbar)).ShouldBeEmpty();
        await toolbar.OpenFilterAsync();
        await Expect(toolbar.SheetRow(name).GetByTestId(TaskboardTestIds.FilterSheetCount)).ToHaveTextAsync(count.ToString());
        (await TouchRules.ViolationsAsync(Page, Page.GetByTestId(TouchTestIds.Sheet))).ShouldBeEmpty();

        await toolbar.SheetRow(name).TapAsync();
        await toolbar.SheetClose.TapAsync();

        await Expect(toolbar.FilterButton).ToHaveTextAsync("Filter (1)");
        var cards = Page.GetByTestId(TaskboardTestIds.Card);
        await Expect(cards.First).ToBeAttachedAsync();
        var assignees = await cards.GetByTestId(TaskboardTestIds.CardAssignee).GetByTestId(ComponentTestIds.Avatar).EvaluateAllAsync<string[]>("els => els.map(e => e.getAttribute('title'))");
        assignees.Distinct().ShouldBe([name]);
        (await cards.CountAsync()).ShouldBe(assignees.Length);
    }

    [Test]
    [Order(2)]
    public async Task FeatureBoard_WhenStoryAddedOnPhone_ShouldListIt()
    {
        var grid = new PhoneGridPart(Page);
        var toolbar = new PhoneToolbarPart(Page);
        await Page.GotoAsync(Links.FeatureBoard(Project, Feature));
        await grid.WaitAsync();

        var created = await toolbar.AddStoryAsync(NewStory);

        created.Ok.ShouldBeTrue(await created.TextAsync());
        await Expect(grid.LaneTitled(grid.Pages.First, NewStory)).ToHaveCountAsync(1);
        (await TouchRules.ViolationsAsync(Page, Page.GetByTestId(TaskboardTestIds.AddStoryBox))).ShouldBeEmpty();
    }

    // The first person with a task in an open story (closed stories are one line, without cards): their name and their
    // task count on the board, from the API.
    private async Task<(string Name, int Count)> FirstAssignedPersonAsync()
    {
        var response = await Page.Context.APIRequest.GetAsync($"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{Feature}/feature-board");
        response.Ok.ShouldBeTrue(await response.TextAsync());
        var view = JsonDocument.Parse(await response.TextAsync()).RootElement;
        var onCards = new HashSet<string>();
        foreach (var row in view.GetProperty("rows").EnumerateArray())
        {
            var parent = row.GetProperty("parent");
            if (parent.ValueKind == JsonValueKind.Object && parent.GetProperty("state").GetProperty("category").GetString() == "completed")
            {
                continue;
            }
            foreach (var task in row.GetProperty("cells").EnumerateArray().SelectMany(cell => cell.GetProperty("tasks").EnumerateArray()))
            {
                if (task.GetProperty("assignee") is { ValueKind: JsonValueKind.Object } assignee)
                {
                    onCards.Add(assignee.GetProperty("name").GetString()!);
                }
            }
        }
        foreach (var row in view.GetProperty("peopleRows").EnumerateArray())
        {
            if (row.GetProperty("person") is { ValueKind: JsonValueKind.Object } person && onCards.Contains(person.GetProperty("name").GetString()!))
            {
                return (person.GetProperty("name").GetString()!, row.GetProperty("taskCount").GetInt32());
            }
        }
        throw new InvalidOperationException("No assigned task on an open story.");
    }
}
