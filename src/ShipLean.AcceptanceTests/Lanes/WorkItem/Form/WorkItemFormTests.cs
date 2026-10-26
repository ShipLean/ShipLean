using System.Text.RegularExpressions;
using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Form;

[TestFixture]
public class WorkItemFormTests : SeededAcceptanceTest
{
    private async Task<int> RevisionOfAsync(string itemKey)
    {
        var response = await Page.Context.APIRequest.GetAsync(
            $"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{itemKey}");
        response.Ok.ShouldBeTrue();
        using var json = JsonDocument.Parse(await response.TextAsync());
        return json.RootElement.GetProperty("revision").GetInt32();
    }

    [Test]
    public async Task WorkItem_WhenSaved_ShouldWriteOneRevision()
    {
        var screen = new WorkItemScreen(Page);
        await screen.OpenAsync("SHIP", "SHIP-141");
        var before = await RevisionOfAsync("SHIP-141");

        await screen.Points.FillAsync("13");
        await screen.AddTagAsync("Acceptance");
        var saved = await screen.SaveAsync(SeedIds.WorkItems.Ship141);

        saved.Ok.ShouldBeTrue();
        await Expect(screen.SaveState).ToHaveTextAsync($"Saved · revision {before + 1}");
        (await RevisionOfAsync("SHIP-141")).ShouldBe(before + 1);
        await Expect(screen.Save).ToBeDisabledAsync();
        await Expect(screen.Points).ToHaveValueAsync("13");
    }

    [Test]
    public async Task WorkItem_WhenLeavingDirty_ShouldBlockWithToast()
    {
        var screen = new WorkItemScreen(Page);
        await screen.OpenAsync("SHIP", "SHIP-141");

        await screen.Points.FillAsync("21");
        await screen.Back.ClickAsync();

        await Expect(screen.Toast).ToContainTextAsync("unsaved changes");
        await Expect(Page).ToHaveURLAsync(new Regex("/p/SHIP/items/SHIP-141$"));
        await Expect(screen.Points).ToHaveValueAsync("21");
    }

    [Test]
    public async Task WorkItem_WhenBug_ShouldShowReproAndSeverity()
    {
        var screen = new WorkItemScreen(Page);

        await screen.OpenAsync("SHIP", "SHIP-153");

        await Expect(screen.Repro).ToBeVisibleAsync();
        await Expect(screen.Severity).ToBeVisibleAsync();
        await Expect(screen.Criteria).ToHaveCountAsync(0);
    }
}
