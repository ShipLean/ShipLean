using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Feature;
using ShipLean.AcceptanceTests.Pages.Shell;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Mcp;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Form;

// The routed work item page in the real shell: long link titles and addresses, with the Add link form open, must not
// make the pane wider than itself or pan it sideways. The same goes for a long unbroken agent name in the History rail.
[TestFixture]
public class WorkItemOverflowTests : SeededAcceptanceTest
{
    // One class shares one database, so each width gets an address of its own.
    private static string LongTitle(int width) => $"A very long link title that has no good place to break {width} " + new string('t', 200);

    // The longest name an agent can register under (200 characters), with no place to break.
    private static readonly string LongAgentName = new('W', 200);

    // The longest display name a person can have (100 characters), with no place to break.
    private static readonly string LongDisplayName = new('W', 100);

    private static string LongUrl(int width) => $"https://example.com/{width}/" + new string('u', 300);

    [TestCase(1024)]
    [TestCase(1440)]
    public async Task StoryPage_WhenLinksAreLongAndAddLinkOpen_ShouldNotOverflowShellMain(int width)
    {
        await Page.SetViewportSizeAsync(width, 900);
        var links = new LinksPanel(Page);
        var screen = new WorkItemScreen(Page);
        await AddLongLinkToStoryAsync(width);

        await screen.OpenAsync("SHIP", "SHIP-147");
        await Expect(links.Rows.Filter(new() { HasText = LongTitle(width) })).ToHaveCountAsync(1);
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
        var created = await Page.Context.APIRequest.PostAsync($"/api/work-items/{SeedIds.WorkItems.Ship101}/link-cards", new APIRequestContextOptions
        {
            DataObject = new { url = LongUrl(width), title = LongTitle(width) },
        });
        created.Ok.ShouldBeTrue(await created.TextAsync());

        await screen.OpenAsync("SHIP", "SHIP-101");
        await Expect(section.Section).ToBeVisibleAsync();
        await Expect(section.CardTitled(LongTitle(width))).ToHaveCountAsync(1);
        await add.Open.ClickAsync();
        await Expect(add.Form).ToBeVisibleAsync();

        await AssertMainDoesNotOverflowAsync();
    }

    [TestCase("SHIP-147", 1024)]
    [TestCase("SHIP-147", 1440)]
    [TestCase("SHIP-101", 1024)]
    [TestCase("SHIP-101", 1440)]
    public async Task WorkItemPage_WhenHistoryAgentNameIsLongAndUnbroken_ShouldNotOverflowShellMain(string key, int width)
    {
        await Page.SetViewportSizeAsync(width, 900);
        var side = new SideRailPanel(Page);
        var screen = new WorkItemScreen(Page);
        var token = await Host.IssueAgentToken(TestUser.Joel, new HashSet<string> { "shiplean:read", "shiplean:write" }, LongAgentName);
        await using var mcp = await McpTestClient.ConnectAsync(new Uri($"{BaseUrl}/mcp"), token);
        var refined = await mcp.CallToolAsync("refine_work_item", new Dictionary<string, object?> { ["item"] = key, ["title"] = $"History overflow check {key} {width}" });
        refined.IsError.ShouldNotBe(true, refined.StructuredContent?.ToString());

        await screen.OpenAsync("SHIP", key);
        await Expect(side.Revisions.First.GetByTestId(WorkItemTestIds.RevisionWho)).ToContainTextAsync(LongAgentName);

        await AssertMainDoesNotOverflowAsync();
    }

    [TestCase("SHIP-147", 1024)]
    [TestCase("SHIP-147", 1440)]
    [TestCase("SHIP-101", 1024)]
    [TestCase("SHIP-101", 1440)]
    public async Task WorkItemPage_WhenDisplayNameIsLongAndUnbroken_ShouldNotOverflowShellMain(string key, int width)
    {
        await Page.SetViewportSizeAsync(width, 900);
        var screen = new WorkItemScreen(Page);
        var me = await Page.Context.APIRequest.GetAsync("/api/me");
        me.Ok.ShouldBeTrue(await me.TextAsync());
        using var current = JsonDocument.Parse(await me.TextAsync());
        var originalName = current.RootElement.GetProperty("name").GetString()!;
        // The seeded items were created and last changed by Priya; Joel's save makes him the one who changed it.
        var itemId = key == "SHIP-147" ? SeedIds.WorkItems.Ship147 : SeedIds.WorkItems.Ship101;
        var itemResponse = await Page.Context.APIRequest.GetAsync($"/api/work-items/{itemId}");
        itemResponse.Ok.ShouldBeTrue(await itemResponse.TextAsync());
        using var item = JsonDocument.Parse(await itemResponse.TextAsync());
        var saved = await Page.Context.APIRequest.PatchAsync($"/api/work-items/{itemId}", new APIRequestContextOptions
        {
            DataObject = new
            {
                expectedRevision = item.RootElement.GetProperty("revision").GetInt32(),
                changes = new { title = new { value = $"Display name overflow check {key} {width}" }, addLinks = Array.Empty<object>(), removeLinkIds = Array.Empty<string>() },
            },
        });
        saved.Ok.ShouldBeTrue(await saved.TextAsync());
        try
        {
            await RenameMeAsync(LongDisplayName);
            await screen.OpenAsync("SHIP", key);
            await Expect(Page.GetByTestId(WorkItemTestIds.Meta)).ToContainTextAsync(LongDisplayName);

            await AssertMainDoesNotOverflowAsync();
        }
        finally
        {
            // One class shares one database: put Joel's name back for the other cases. The seeded colour is not one the
            // profile accepts, so the name goes back with a colour from the list.
            await RenameMeAsync(originalName);
        }
    }

    private async Task RenameMeAsync(string name)
    {
        var response = await Page.Context.APIRequest.PutAsync("/api/me/profile", new APIRequestContextOptions { DataObject = new { name, avatarColour = "#7b4fc9" } });
        response.Ok.ShouldBeTrue(await response.TextAsync());
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

    private async Task AddLongLinkToStoryAsync(int width)
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
                    addLinks = new[] { new { type = "url", workItemId = (Guid?)null, key = (string?)null, url = LongUrl(width), title = LongTitle(width) } },
                    removeLinkIds = Array.Empty<string>(),
                },
            },
        });
        saved.Ok.ShouldBeTrue(await saved.TextAsync());
    }
}
