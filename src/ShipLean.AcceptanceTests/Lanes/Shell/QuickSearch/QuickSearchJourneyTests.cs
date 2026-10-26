using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.ItemPeek;
using ShipLean.AcceptanceTests.Pages.QuickSearch;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.Shell;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Shell.QuickSearch;

// Quick search end to end: real search results from a full index pass, the item opening rule, recents, the theme toggle
// and Create bug. The seed has work items only in SHIP, so one PAY item is created through the API before the first test.
[TestFixture]
public class QuickSearchJourneyTests : SeededAcceptanceTest
{
    // No SHIP item or sprint has this word.
    private const string PayWord = "Quokka";
    private const string SeedWord = "passkey";
    private const string PasskeyItem = "SHIP-129";

    private bool _arranged;
    private string _payKey = "";

    [SetUp]
    public async Task ArrangePayItemAndIndexAsync()
    {
        if (_arranged)
        {
            return;
        }

        var created = await Page.Context.APIRequest.PostAsync("/api/work-items", new APIRequestContextOptions
        {
            DataObject = new
            {
                projectId = SeedIds.Projects.Pay,
                type = "story",
                title = $"{PayWord} reconciliation ledger",
                parentId = (Guid?)null,
                sprintId = (Guid?)null,
                changes = (object?)null,
            },
        });
        created.Ok.ShouldBeTrue(await created.TextAsync());
        _payKey = JsonDocument.Parse(await created.TextAsync()).RootElement.GetProperty("key").GetString()!;
        _payKey.ShouldStartWith("PAY-");

        using var scope = Host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISearchIndexer>().RunPassAsync(true);
        _arranged = true;
    }

    [Test]
    public async Task QuickSearch_WhenWordTypedOnABoard_ShouldOpenTheItemInTheModal()
    {
        var search = new QuickSearchDialog(Page);
        var peek = new ItemPeekDialog(Page);
        var board = Links.Board("SHIP", "stories");
        await new ShellFrame(Page).OpenAsync(board);
        await search.SearchButton.FocusAsync();
        await search.OpenWithShortcutAsync();

        await search.TypeAsync(SeedWord);
        await search.PickAsync("item", PasskeyItem);

        await Expect(Page).ToHaveURLAsync(new Regex($"{Regex.Escape(board)}\\?item={PasskeyItem}$"));
        await peek.WaitOpenAsync(PasskeyItem);
        await Expect(search.Dialog).ToHaveCountAsync(0);
        await peek.Close.ClickAsync();
        await peek.WaitClosedAsync();
        // Catches focus left on the body after the modal closes: the modal's opener (quick search's input) is gone.
        await Expect(search.SearchButton).ToBeFocusedAsync();
    }

    [Test]
    public async Task QuickSearch_WhenPickedOnABacklog_ShouldOpenTheFullItemPage()
    {
        var search = new QuickSearchDialog(Page);
        await new ShellFrame(Page).OpenAsync(Links.Backlog("SHIP"));
        await search.OpenWithShortcutAsync();

        await search.TypeAsync(SeedWord);
        await search.PickAsync("item", PasskeyItem);

        await Expect(Page).ToHaveURLAsync(new Regex($"{Regex.Escape(Links.WorkItem("SHIP", PasskeyItem))}$"));
        await Expect(Page.GetByTestId(WorkItemTestIds.Key)).ToHaveTextAsync(PasskeyItem);
        // A Backlogs page never shows the modal. Catches an item that opens in the modal over the backlog.
        await Expect(new ItemPeekDialog(Page).Dialog).ToHaveCountAsync(0);
    }

    [Test]
    public async Task QuickSearch_WhenAnotherProjectsItemPicked_ShouldOpenItsFullPage()
    {
        var search = new QuickSearchDialog(Page);
        await new ShellFrame(Page).OpenAsync(Links.Board("SHIP", "stories"));
        await search.OpenWithShortcutAsync();

        await search.TypeAsync(PayWord);
        await search.PickAsync("item", _payKey);

        await Expect(Page).ToHaveURLAsync(new Regex($"{Regex.Escape(Links.WorkItem("PAY", _payKey))}$"));
        await Expect(Page.GetByTestId(WorkItemTestIds.Key)).ToHaveTextAsync(_payKey);
        // Catches a pick that opens another project's item in the modal over a SHIP board.
        await Expect(new ItemPeekDialog(Page).Dialog).ToHaveCountAsync(0);
    }

    [Test]
    public async Task QuickSearch_WhenAnItemWasOpened_ShouldListItUnderRecent()
    {
        var search = new QuickSearchDialog(Page);
        var peek = new ItemPeekDialog(Page);
        var board = Links.Board("SHIP", "stories");
        await OpenAndCloseAsync(peek, board, "SHIP-147");
        await OpenAndCloseAsync(peek, board, "SHIP-144");

        await search.OpenWithShortcutAsync();

        // Newest first. Catches a list that keeps the oldest first, or one that never recorded the item.
        await Expect(search.Options.First).ToHaveAttributeAsync("data-key", "SHIP-144");
        await Expect(search.Options.Nth(1)).ToHaveAttributeAsync("data-key", "SHIP-147");
        await Expect(search.Groups.First).ToHaveAttributeAsync("data-group", "recent");
    }

    [Test]
    public async Task QuickSearch_WhenARecentItemIsRenamed_ShouldListItOnceWithTheNewTitle()
    {
        var search = new QuickSearchDialog(Page);
        var peek = new ItemPeekDialog(Page);
        var board = Links.Board("SHIP", "stories");
        await OpenAndCloseAsync(peek, board, "SHIP-141");
        await new LiveStatus(Page).WaitUntilLiveAsync();

        var item = await GetItemAsync("SHIP-141");
        var newTitle = "Pay with a saved card, renamed";
        var patch = await Page.Context.APIRequest.PatchAsync($"/api/work-items/{item.GetProperty("id").GetGuid()}", new APIRequestContextOptions
        {
            DataObject = new
            {
                expectedRevision = item.GetProperty("revision").GetInt32(),
                changes = new { title = new { value = newTitle }, addLinks = Array.Empty<object>(), removeLinkIds = Array.Empty<string>() },
            },
        });
        patch.Ok.ShouldBeTrue(await patch.TextAsync());
        (await GetItemAsync("SHIP-141")).GetProperty("title").GetString().ShouldBe(newTitle);

        // Open it again from the page, so the item is fetched fresh, and close it.
        await search.OpenWithShortcutAsync();
        await search.TypeAsync("saved card");
        await search.PickAsync("item", "SHIP-141");
        await peek.WaitOpenAsync("SHIP-141");
        await Expect(peek.Title).ToHaveValueAsync(newTitle);
        await peek.Close.ClickAsync();
        await peek.WaitClosedAsync();
        await search.OpenWithShortcutAsync();
        // Quick search keeps the last query while closed, so clear it to see the recent items.
        await search.Input.ClearAsync();

        var rows = search.Option("item", "SHIP-141");
        // Catches the kept query still showing search results instead of the recent items.
        await Expect(search.Groups.First).ToHaveAttributeAsync("data-group", "recent");
        // Catches a stale title (the entry kept as first written).
        await Expect(search.Options.First).ToContainTextAsync(newTitle);
        // Catches a duplicate row (the renamed item added beside the old entry).
        await Expect(rows).ToHaveCountAsync(1);
    }

    [Test]
    public async Task QuickSearch_WhenToggleThemePicked_ShouldKeepTheThemeAfterReload()
    {
        var search = new QuickSearchDialog(Page);
        await new ShellFrame(Page).OpenAsync(ShellFrame.BacklogPath("SHIP"));
        await search.OpenWithShortcutAsync();
        // The toggle is offered once the saved theme has loaded.
        await search.Option("action", "toggle-theme").WaitForAsync();

        // The browser prefers light, so the toggle goes to dark.
        var saved = await Page.RunAndWaitForApiAsync("/api/me/preferences/theme", "PUT", () => search.PickAsync("action", "toggle-theme"));
        saved.Status.ShouldBe(204);
        await Page.WaitForFunctionAsync("document.documentElement.dataset.theme === 'dark'");

        await Page.ReloadAsync();
        await new ShellFrame(Page).SideMenu.WaitForAsync();
        // Catches a toggle that only changes the page and never saves.
        await Page.WaitForFunctionAsync("document.documentElement.dataset.theme === 'dark'");
    }

    [Test]
    public async Task QuickSearch_WhenCreateBugPickedOnTheStoriesBacklog_ShouldOpenTheNewRowWithBug()
    {
        var search = new QuickSearchDialog(Page);
        var backlog = new Pages.Backlog.StoriesBacklog(Page);
        await backlog.OpenAsync("SHIP");
        await search.OpenWithShortcutAsync();

        await search.PickAsync("action", "create-bug");

        // The page was already mounted, so this proves the backlog reacts to the new link. Catches a backlog that reads
        // the link only on first render, which would leave the row closed.
        await Expect(backlog.AddRow).ToBeVisibleAsync();
        await Expect(backlog.AddType).ToHaveValueAsync("bug");
        // Catches a link left in the address, which would reopen the row on every refresh.
        await Expect(Page).Not.ToHaveURLAsync(new Regex("[?&]new="));
    }

    [Test]
    public async Task QuickSearch_WhenSprintPicked_ShouldOpenItsTaskboard()
    {
        var search = new QuickSearchDialog(Page);
        await new ShellFrame(Page).OpenAsync(Links.Backlog("SHIP"));
        await search.OpenWithShortcutAsync();

        var taskboard = Links.Taskboard("SHIP", SeedIds.Sprints.Sprint24);
        await search.TypeAsync(SeedWord);
        await search.PickAsync("sprint", taskboard);

        await Expect(Page).ToHaveURLAsync(new Regex($"{Regex.Escape(taskboard)}$"));
    }

    private async Task OpenAndCloseAsync(ItemPeekDialog peek, string page, string itemKey)
    {
        await Page.GotoAsync(Links.WithItem(page, itemKey));
        await peek.WaitOpenAsync(itemKey);
        await peek.Close.ClickAsync();
        await peek.WaitClosedAsync();
    }

    private async Task<JsonElement> GetItemAsync(string itemKey)
    {
        var response = await Page.Context.APIRequest.GetAsync($"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{itemKey}");
        response.Ok.ShouldBeTrue();
        return JsonDocument.Parse(await response.TextAsync()).RootElement.Clone();
    }
}
