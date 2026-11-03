using Dapper;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.AcceptanceTests.Pages.Shell;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Retros;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Journeys;

// The Retrospectives Features screen and the item page behind it: from the side menu to a Feature, from a work item's row to
// the item page filtered to that retro, and the Add action dialog and the search filters on the same data.
[TestFixture]
public class RetroItemJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";

    [Test]
    public async Task Features_WhenOpenedFromTheSideMenu_ShouldListCountsAndOpenTheItem()
    {
        var feature = await ArrangeFeatureAsync("Menu feature");
        var story = await ArrangeLooseAsync("story", "Menu loose story", RetroCategory.Improve, "Loose story improve point");
        var bug = await ArrangeLooseAsync("bug", "Menu loose bug", RetroCategory.WentWell, "Loose bug went well point");
        var shell = new ShellFrame(Page);
        await shell.OpenAsync(ShellFrame.BacklogPath(Project));

        await shell.NavRetrospectives.ClickAsync();
        await Expect(shell.NavRetrosFeatures).ToBeVisibleAsync();
        var search = await shell.NavRetrosSearch.BoundingBoxAsync();
        var features = await shell.NavRetrosFeatures.BoundingBoxAsync();
        var insights = await shell.NavRetrosInsights.BoundingBoxAsync();
        search!.Y.ShouldBeLessThan(features!.Y);
        features.Y.ShouldBeLessThan(insights!.Y);

        await shell.NavRetrosFeatures.ClickAsync();
        var page = new RetroFeaturesPage(Page);
        await page.Panel.WaitForAsync();
        var row = page.RowOf(feature.Title);
        await Expect(row).ToHaveCountAsync(1);
        // The Feature has no retro of its own: its counts are its story's.
        await Expect(RetroFeaturesPage.CountOf(row, "wentBadly")).ToHaveAttributeAsync("title", "Went badly: 2");
        await Expect(RetroFeaturesPage.CountOf(row, "wentWell")).ToHaveAttributeAsync("title", "Went well: 1");
        await Expect(page.LooseHeading).ToBeVisibleAsync();
        await Expect(page.RowOf(story.Title)).ToHaveCountAsync(1);
        await Expect(page.RowOf(bug.Title)).ToHaveCountAsync(1);

        // To decide keeps what has points to decide: the bug has only a Went well point.
        await page.FilterOption("To decide").ClickAsync();
        await Expect(page.RowOf(feature.Title)).ToHaveCountAsync(1);
        await Expect(page.RowOf(story.Title)).ToHaveCountAsync(1);
        await Expect(page.RowOf(bug.Title)).ToHaveCountAsync(0);

        await RetroFeaturesPage.OpenLinkOf(page.RowOf(feature.Title)).ClickAsync();
        var item = new RetroItemPage(Page);
        await item.Panel.WaitForAsync();
        await Expect(item.Panel).ToHaveAttributeAsync("data-item", feature.Key);
        await Expect(item.ChildRows.Filter(new() { HasText = feature.ChildTitle })).ToHaveCountAsync(1);
    }

    [Test]
    public async Task Item_WhenFeature_ShouldShowBoardRailLogsAndStoryRows()
    {
        var feature = await ArrangeFeatureAsync("Item feature", withOwnRetro: true);
        var item = new RetroItemPage(Page);

        await item.OpenAsync(Project, feature.Key);

        await Expect(item.Board.NoteOf("Compiled improve point").Root).ToHaveCountAsync(1);
        await Expect(item.Rail).ToBeVisibleAsync();
        await Expect(item.Compiled).ToContainTextAsync(feature.ChildKey);
        await Expect(item.RailActions).ToContainTextAsync("Item feature action");
        await Expect(item.Writers).ToContainTextAsync("Conductor");
        await Expect(item.Log).ToHaveCountAsync(0);
        await item.LogToggle.First.ClickAsync();
        await Expect(item.Log.First).ToContainTextAsync("Feature log line");
        await Expect(item.ChildRows).ToHaveCountAsync(1);
        await Expect(item.ChildRowOf(feature.ChildTitle)).ToContainTextAsync(feature.ChildKey);
    }

    [Test]
    public async Task Item_WhenStoryWithNoFeature_ShouldWork()
    {
        var story = await ArrangeLooseAsync("story", "Item loose story", RetroCategory.WentBadly, "Story went badly point");
        var item = new RetroItemPage(Page);

        await item.OpenAsync(Project, story.Key);

        await Expect(item.Board.NoteOf("Story went badly point").Root).ToHaveCountAsync(1);
        await Expect(item.Panel).ToContainTextAsync(story.Title);
        await Expect(item.Panel).Not.ToContainTextAsync("Part of");
        await Expect(item.ChildRowsSection).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Item_WhenBugWithNoFeature_ShouldWork()
    {
        var bug = await ArrangeLooseAsync("bug", "Item loose bug", RetroCategory.NotInPlan, "Bug not in plan point");
        var item = new RetroItemPage(Page);

        await item.OpenAsync(Project, bug.Key);

        await Expect(item.Board.NoteOf("Bug not in plan point").Root).ToHaveCountAsync(1);
        await Expect(item.Panel).ToContainTextAsync(bug.Title);
        await Expect(item.Panel).Not.ToContainTextAsync("Part of");
        await Expect(item.ChildRowsSection).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Item_WhenBugInAFeature_ShouldShowItsPartOfLine()
    {
        await using var connection = await OpenAsync();
        var featureId = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Bug parent feature").InsertAsync(connection);
        var bugId = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("bug").WithTitle("Bug under a feature").WithParent(featureId).InsertAsync(connection);
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: bugId, AgentName: "bug-agent"));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Bug in a feature point"));
        var featureKey = await KeyAsync(connection, featureId);
        var bugKey = await KeyAsync(connection, bugId);
        var item = new RetroItemPage(Page);

        await item.OpenAsync(Project, bugKey);

        await Expect(item.Panel).ToContainTextAsync($"Part of {featureKey} Bug parent feature");
    }

    [Test]
    public async Task WorkItemRow_WhenClicked_ShouldOpenTheItemPageFilteredToThatRetro()
    {
        var story = await ArrangeTwoRetrosAsync();
        await Page.GotoAsync(Links.WorkItem(Project, story.Key));
        var section = new WorkItemRetroSection(Page);
        await Expect(section.Rows).ToHaveCountAsync(2);

        // A click on the row itself, not on its author link.
        await section.Rows.Filter(new() { HasText = "agent-two" }).ClickAsync();

        var item = new RetroItemPage(Page);
        await item.Panel.WaitForAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"/retros/items/{story.Key}\\?retro="));
        await Expect(item.Board.NoteOf("Point of agent two").Root).ToHaveCountAsync(1);
        await Expect(item.Board.NoteOf("Point of agent one").Root).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ItemToolbarAction_ShouldCreateAnActionItemFromTheDialog()
    {
        var story = await ArrangeLooseAsync("story", "Toolbar story", RetroCategory.Improve, "Toolbar improve point");
        var item = new RetroItemPage(Page);
        await item.OpenAsync(Project, story.Key);

        await item.AddAction.ClickAsync();
        await Expect(item.Dialog.Dialog).ToBeVisibleAsync();
        await item.Dialog.Pick.Filter(new() { HasText = "Toolbar improve point" }).ClickAsync();
        await item.Dialog.Title.FillAsync("Toolbar action item");
        var created = await Page.RunAndWaitForResponseAsync(
            () => item.Dialog.Save.ClickAsync(),
            r => r.Request.Method == "POST" && new Uri(r.Url).AbsolutePath.EndsWith("/retros/actions", StringComparison.Ordinal));
        created.Ok.ShouldBeTrue(await created.TextAsync());

        await Expect(item.Dialog.Dialog).ToHaveCountAsync(0);
        await Expect(item.RailActions).ToContainTextAsync("Toolbar action item");
    }

    [Test]
    public async Task Search_WhenUnthemedAndDecisionFiltersUsed_ShouldNarrowAndSelectedHasButtons()
    {
        await using (var connection = await OpenAsync())
        {
            var storyId = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Search filter story").InsertAsync(connection);
            var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
            var theme = await seed.ThemeAsync("Search filter theme");
            var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: storyId, AgentName: "search-agent"));
            await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Zebra themed point", ThemeId: theme));
            await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Zebra unthemed undecided"));
            await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Zebra unthemed not needed", NotNeededReason: "Fixed already"));
        }

        var search = new RetrosSearchPage(Page);
        await search.OpenAsync(Project, ("q", "Zebra"));
        await Expect(search.Row).ToHaveCountAsync(3);

        await search.FilterUnthemed.ClickAsync();
        await Expect(search.FilterUnthemed).ToBeCheckedAsync();
        await Expect(search.Row).ToHaveCountAsync(2);
        await search.FilterDecision.SelectOptionAsync("undecided");
        await Expect(search.Row).ToHaveCountAsync(1);
        await Expect(search.RowOf("Zebra unthemed undecided")).ToHaveCountAsync(1);

        await search.RowOf("Zebra unthemed undecided").ClickAsync();
        var note = new RetroBoardPart(search.Selected).NoteOf("Zebra unthemed undecided");
        await Expect(note.Root).ToBeVisibleAsync();
        await Expect(note.AddAction).ToBeVisibleAsync();
        await Expect(note.NotNeeded).ToBeVisibleAsync();
    }

    private sealed record Arranged(string Key, string Title, string ChildKey = "", string ChildTitle = "");

    // A Feature with one story; the story has a retro with two Went badly points and a Went well one. With `withOwnRetro`
    // the Feature also has a conductor's retro compiled from the story's, a failure log and an action item.
    private async Task<Arranged> ArrangeFeatureAsync(string title, bool withOwnRetro = false)
    {
        await using var connection = await OpenAsync();
        var storyTitle = $"{title} story";
        var featureId = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle(title).InsertAsync(connection);
        var storyId = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle(storyTitle).WithParent(featureId).InsertAsync(connection);
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var storyRetro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: storyId, AgentName: "story-agent"));
        await seed.PointAsync(storyRetro, new RetroSeedPoint(RetroCategory.WentBadly, $"{title} bad one"));
        await seed.PointAsync(storyRetro, new RetroSeedPoint(RetroCategory.WentBadly, $"{title} bad two"));
        await seed.PointAsync(storyRetro, new RetroSeedPoint(RetroCategory.WentWell, $"{title} good one"));
        if (withOwnRetro)
        {
            var own = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Conductor, WorkItemId: featureId, AgentName: "Conductor"));
            var improve = await seed.PointAsync(own, new RetroSeedPoint(RetroCategory.Improve, "Compiled improve point"));
            await seed.SourceAsync(own, storyRetro);
            await seed.FailureAsync(own, RetroFailureKind.Friction, "Feature log line");
            var action = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle($"{title} action").InsertAsync(connection);
            await seed.ActionAsync(new RetroSeedAction(action, PointId: improve));
        }

        return new Arranged(await KeyAsync(connection, featureId), title, await KeyAsync(connection, storyId), storyTitle);
    }

    // A story or bug with no Feature and one retro holding one point.
    private async Task<Arranged> ArrangeLooseAsync(string type, string title, RetroCategory category, string pointText)
    {
        await using var connection = await OpenAsync();
        var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType(type).WithTitle(title).InsertAsync(connection);
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: id, AgentName: $"{type}-agent"));
        await seed.PointAsync(retro, new RetroSeedPoint(category, pointText));
        return new Arranged(await KeyAsync(connection, id), title);
    }

    private async Task<Arranged> ArrangeTwoRetrosAsync()
    {
        await using var connection = await OpenAsync();
        var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Two retro story").InsertAsync(connection);
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var one = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: id, AgentName: "agent-one", CreatedAt: new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero)));
        await seed.PointAsync(one, new RetroSeedPoint(RetroCategory.Improve, "Point of agent one"));
        var two = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: id, AgentName: "agent-two", CreatedAt: new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero)));
        await seed.PointAsync(two, new RetroSeedPoint(RetroCategory.Improve, "Point of agent two"));
        return new Arranged(await KeyAsync(connection, id), "Two retro story");
    }

    private static async Task<string> KeyAsync(NpgsqlConnection connection, Guid id) =>
        (await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }))!;

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}
