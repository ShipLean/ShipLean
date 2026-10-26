using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.List;

// The Stories backlog in list mode on the seeded Storefront project (SHIP), signed in as Joel. Each test changes
// something the next ones don't read: a new item at the top, one hidden column.
[TestFixture]
public class StoriesListTests : SeededAcceptanceTest
{
    [Test]
    [Order(1)]
    public async Task Backlog_WhenNewItemAdded_ShouldBeFirstWithNextKey()
    {
        var response = await Page.Context.APIRequest.GetAsync("/api/projects/by-key/SHIP");
        using var project = JsonDocument.Parse(await response.TextAsync());
        var expectedKey = $"SHIP-{project.RootElement.GetProperty("nextItemNumber").GetInt32()}";

        var backlog = new StoriesBacklog(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        await backlog.NewItem.ClickAsync();
        await Expect(backlog.AddRow).ToBeVisibleAsync();
        await backlog.AddAsync("Offline mode for the shop");

        await Expect(backlog.RowKeys.First).ToHaveTextAsync(expectedKey);
        await Expect(backlog.Rows.First).ToContainTextAsync("Offline mode for the shop");
        await Expect(backlog.AddRow).ToBeVisibleAsync();
    }

    [Test]
    [Order(2)]
    public async Task Backlog_WhenColumnsChanged_ShouldPersistPerUser()
    {
        var backlog = new StoriesBacklog(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.ColumnHeader("Parent")).ToBeVisibleAsync();

        await backlog.ToggleColumnAsync("Parent");
        await Expect(backlog.ColumnHeader("Parent")).ToHaveCountAsync(0);

        await Page.ReloadAsync();
        await Expect(backlog.ColumnHeaders.First).ToBeVisibleAsync();
        await Expect(backlog.ColumnHeader("Parent")).ToHaveCountAsync(0);
        await Expect(backlog.ColumnHeader("Sprint")).ToBeVisibleAsync();

        await SignOutAsync();
        await SignInAsync(TestUser.Sam.Email!, TestUser.Sam.Password!);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.ColumnHeaders.First).ToBeVisibleAsync();
        await Expect(backlog.ColumnHeader("Parent")).ToBeVisibleAsync();
    }

    [Test]
    [Order(3)]
    public async Task Backlog_WhenProjectEmpty_ShouldOfferFirstStoryAndImport()
    {
        var backlog = new StoriesBacklog(Page);
        await backlog.OpenAsync("EMPT");

        await Expect(backlog.Empty).ToBeVisibleAsync();
        await Expect(backlog.EmptyImport).ToHaveAttributeAsync("href", Links.JiraImport("EMPT"));

        await backlog.EmptyCreate.ClickAsync();
        await Expect(backlog.AddRow).ToBeVisibleAsync();
        await Expect(backlog.Empty).ToHaveCountAsync(0);
    }
}
