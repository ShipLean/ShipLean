using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.AcceptanceTests.Pages.Retros.Touch;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Touch;

// One item's retros on a phone (SHIP-729 R1-R7): ‹ Features in the top bar, the header numbers as buttons, the board in one
// column, the rail as sheets and a Feature's stories as row links. The touch rules are checked on this task's regions only
// (the header buttons, the toolbar and the child rows); the whole screen is the route sweep's.
[TestFixture]
public class RetroItemTouchTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task ItemRetro_WhenOpenedOnAPhone_ShouldStackTheCategoriesAndOpenTheRailAsSheets()
    {
        var feature = await ArrangeFeatureAsync();
        var (item, touch) = await OpenAsync(feature.Key);

        await Expect(touch.Back).ToHaveAttributeAsync("aria-label", "Features");
        await Expect(item.Rail).ToHaveCountAsync(0);
        var xs = new List<double>();
        for (var i = 0; i < await touch.Columns.CountAsync(); i++)
        {
            xs.Add((await touch.Columns.Nth(i).BoundingBoxAsync())!.X);
        }

        xs.ShouldNotBeEmpty();
        xs.Distinct().Count().ShouldBe(1);
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= document.documentElement.clientWidth")).ShouldBeTrue();
        (await TouchRules.ViolationsAsync(Page, touch.KpiToDecide)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, touch.KpiActions)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, touch.RetroSelect)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, item.AddAction)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, touch.More)).ShouldBeEmpty();

        await touch.More.TapAsync();
        await touch.MoreRow("Written by").TapAsync();
        await Expect(touch.Sheet).ToBeVisibleAsync();
        await Expect(touch.SheetWriters).ToContainTextAsync("Conductor");
        await touch.SheetClose.TapAsync();
        await Expect(touch.Sheet).ToHaveCountAsync(0);

        await touch.KpiActions.TapAsync();
        await Expect(touch.SheetActions).ToContainTextAsync(feature.ActionTitle);
        await touch.SheetClose.TapAsync();
        await Expect(touch.Sheet).ToHaveCountAsync(0);

        await touch.Back.TapAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/retros/features$"));
    }

    [Test]
    public async Task ItemRetro_WhenFilteredOnAPhone_ShouldKeepRetroAndDecideInTheAddress()
    {
        var story = await ArrangeTwoRetrosAsync();
        var (item, touch) = await OpenAsync(story.Key);
        (await touch.RetroSelect.EvaluateAsync<string>("e => getComputedStyle(e).fontSize")).ShouldBe("16px");
        await Expect(item.Board.NoteOf("Phone point one").Root).ToHaveCountAsync(1);
        await Expect(item.Board.NoteOf("Phone point two").Root).ToHaveCountAsync(1);

        await touch.RetroSelect.SelectOptionAsync(new SelectOptionValue { Label = "agent-two · subAgent" });
        await Expect(Page).ToHaveURLAsync(new Regex("retro="));
        await Expect(item.Board.NoteOf("Phone point one").Root).ToHaveCountAsync(0);
        await Expect(item.Board.NoteOf("Phone point two").Root).ToHaveCountAsync(1);

        await touch.RetroSelect.SelectOptionAsync(new SelectOptionValue { Label = "All retros" });
        Page.Url.ShouldNotContain("retro=");
        await Expect(touch.KpiToDecide).ToHaveAttributeAsync("aria-pressed", "false");
        await touch.KpiToDecide.TapAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("decide=open"));
        await Expect(touch.KpiToDecide).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(item.Board.NoteOf("Phone point one").Root).ToHaveCountAsync(1);
        await Expect(item.Board.NoteOf("Phone point two").Root).ToHaveCountAsync(0);
    }

    [Test]
    public async Task FeatureRetro_WhenOpenedOnAPhone_ShouldListChildRowsThatOpenTheirRetros()
    {
        var feature = await ArrangeFeatureAsync();
        var (item, _) = await OpenAsync(feature.Key);

        var row = item.ChildRowOf(feature.ChildTitle);
        await Expect(row).ToHaveCountAsync(1);
        await Expect(row).ToContainTextAsync(feature.ChildKey);
        await Expect(row).ToContainTextAsync("to decide");
        (await row.BoundingBoxAsync())!.Height.ShouldBeGreaterThanOrEqualTo(32);
        (await TouchRules.ViolationsAsync(Page, row)).ShouldBeEmpty();

        await row.TapAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"/retros/items/{feature.ChildKey}$"));
        await item.Panel.WaitForAsync();
    }

    [Test]
    public async Task ItemRetro_WhenActionItemTapped_ShouldOpenTheSharedDialog()
    {
        var story = await ArrangeTwoRetrosAsync();
        var (item, _) = await OpenAsync(story.Key);

        await item.AddAction.TapAsync();
        await Expect(item.Dialog.Dialog).ToBeVisibleAsync();
    }

    // What each test seeds is settled afterwards (its points archived), so none leaves points to decide behind for the next.
    [TearDown]
    public async Task SettleSeededPointsAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await connection.ExecuteAsync("UPDATE retro_point SET archived_at = now() WHERE archived_at IS NULL AND text LIKE 'Phone %'");
    }

    private async Task<(RetroItemPage Item, RetroItemTouch Touch)> OpenAsync(string itemKey)
    {
        var item = new RetroItemPage(Page);
        await item.OpenAsync(Project, itemKey);
        return (item, new RetroItemTouch(Page));
    }

    private sealed record Arranged(string Key, string ChildKey = "", string ChildTitle = "", string ActionTitle = "");

    // A Feature with a conductor's retro (compiled from its story's, with an action item) and one story with points to decide.
    private async Task<Arranged> ArrangeFeatureAsync()
    {
        await using var connection = await OpenConnectionAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var storyTitle = $"Phone item story {suffix}";
        var actionTitle = $"Phone item action {suffix}";
        var featureId = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle($"Phone item feature {suffix}").InsertAsync(connection);
        var storyId = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle(storyTitle).WithParent(featureId).InsertAsync(connection);
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var storyRetro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: storyId, AgentName: "story-agent"));
        await seed.PointAsync(storyRetro, new RetroSeedPoint(RetroCategory.WentBadly, $"Phone went badly {suffix}"));
        await seed.PointAsync(storyRetro, new RetroSeedPoint(RetroCategory.WentWell, $"Phone went well {suffix}"));
        var own = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Conductor, WorkItemId: featureId, AgentName: "Conductor"));
        var improve = await seed.PointAsync(own, new RetroSeedPoint(RetroCategory.Improve, $"Phone improve {suffix}"));
        await seed.SourceAsync(own, storyRetro);
        var action = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle(actionTitle).InsertAsync(connection);
        await seed.ActionAsync(new RetroSeedAction(action, PointId: improve));
        return new Arranged(await KeyAsync(connection, featureId), await KeyAsync(connection, storyId), storyTitle, actionTitle);
    }

    // A story with two retros: agent-one's point is undecided, agent-two's is decided (not needed).
    private async Task<Arranged> ArrangeTwoRetrosAsync()
    {
        await using var connection = await OpenConnectionAsync();
        var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle($"Phone two retros {Guid.NewGuid():N}").InsertAsync(connection);
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var one = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: id, AgentName: "agent-one", CreatedAt: new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero)));
        await seed.PointAsync(one, new RetroSeedPoint(RetroCategory.Improve, "Phone point one"));
        var two = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: id, AgentName: "agent-two", CreatedAt: new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero)));
        await seed.PointAsync(two, new RetroSeedPoint(RetroCategory.Improve, "Phone point two", NotNeededReason: "A one-off"));
        return new Arranged(await KeyAsync(connection, id));
    }

    private static async Task<string> KeyAsync(NpgsqlConnection connection, Guid id) =>
        (await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }))!;

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}
