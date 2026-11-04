using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Feature;
using ShipLean.AcceptanceTests.Pages.Shell;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Form;

// The routed work item page in the real shell: long link titles and addresses, with the Add link form open, must not
// make the pane wider than itself or pan it sideways.
[TestFixture]
public class WorkItemOverflowTests : SeededAcceptanceTest
{
    private static readonly string LongTitle = "A very long link title that has no good place to break " + new string('t', 200);

    private static readonly string LongUrl = "https://example.com/" + new string('u', 300);

    [TestCase(1024)]
    [TestCase(1440)]
    public async Task StoryPage_WhenLinksAreLongAndAddLinkOpen_ShouldNotOverflowShellMain(int width)
    {
        await Page.SetViewportSizeAsync(width, 900);
        var links = new LinksPanel(Page);
        var screen = new WorkItemScreen(Page);
        await AddLongLinkToStoryAsync();

        await screen.OpenAsync("SHIP", "SHIP-147");
        await Expect(links.Rows.Filter(new() { HasText = LongTitle })).ToHaveCountAsync(1);
        await Page.GetByTestId(WorkItemTestIds.LinkAdd).ClickAsync();
        await Expect(Page.GetByTestId(WorkItemTestIds.LinkForm)).ToBeVisibleAsync();

        await AssertMainDoesNotOverflowAsync();
    }

    [TestCase(1024)]
    [TestCase(1440)]
    public async Task FeaturePage_WhenLinksAreLongAndAddLinkOpen_ShouldNotOverflowShellMain(int width)
    {
        await Page.SetViewportSizeAsync(width, 900);
        var section = new LinksSectionPanel(Page);
        var add = new AddLinkForm(Page);
        var screen = new WorkItemScreen(Page);
        await screen.OpenAsync("SHIP", "SHIP-101");
        await Expect(section.Section).ToBeVisibleAsync();
        var created = await Page.Context.APIRequest.PostAsync($"/api/work-items/{SeedIds.WorkItems.Ship101}/link-cards", new APIRequestContextOptions
        {
            DataObject = new { url = LongUrl, title = LongTitle },
        });
        created.Ok.ShouldBeTrue(await created.TextAsync());
        await Expect(section.CardTitled(LongTitle)).ToHaveCountAsync(1);

        await add.Open.ClickAsync();
        await Expect(add.Form).ToBeVisibleAsync();

        await AssertMainDoesNotOverflowAsync();
    }

    private async Task AssertMainDoesNotOverflowAsync()
    {
        var main = new ShellFrame(Page).Main;
        await Expect(main).ToBeVisibleAsync();
        var measured = await main.EvaluateAsync<JsonElement>("e => ({ scrollWidth: e.scrollWidth, clientWidth: e.clientWidth, scrollLeft: e.scrollLeft })");
        var scrollWidth = measured.GetProperty("scrollWidth").GetInt32();
        var clientWidth = measured.GetProperty("clientWidth").GetInt32();
        scrollWidth.ShouldBeLessThanOrEqualTo(clientWidth);
        measured.GetProperty("scrollLeft").GetInt32().ShouldBe(0);
    }

    private async Task AddLongLinkToStoryAsync()
    {
        var response = await Page.Context.APIRequest.GetAsync($"/api/work-items/{SeedIds.WorkItems.Ship147}");
        response.Ok.ShouldBeTrue(await response.TextAsync());
        using var item = JsonDocument.Parse(await response.TextAsync());
        var saved = await Page.Context.APIRequest.PatchAsync($"/api/work-items/{SeedIds.WorkItems.Ship147}", new APIRequestContextOptions
        {
            DataObject = new
            {
                expectedRevision = item.RootElement.GetProperty("revision").GetInt32(),
                changes = new
                {
                    title = (object?)null,
                    description = (object?)null,
                    acceptanceCriteria = (object?)null,
                    reproSteps = (object?)null,
                    severity = (object?)null,
                    state = (object?)null,
                    assignee = (object?)null,
                    sprint = (object?)null,
                    priority = (object?)null,
                    points = (object?)null,
                    parent = (object?)null,
                    tags = (object?)null,
                    addLinks = new[] { new { type = "url", workItemId = (Guid?)null, key = (string?)null, url = LongUrl, title = LongTitle } },
                    removeLinkIds = Array.Empty<string>(),
                },
            },
        });
        saved.Ok.ShouldBeTrue(await saved.TextAsync());
    }
}
