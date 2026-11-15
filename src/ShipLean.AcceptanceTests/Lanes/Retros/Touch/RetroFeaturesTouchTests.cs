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
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Touch;

// Retrospectives › Features on a phone (390×844) and at 768×1024 (SHIP-728 D1-D5): one row link per Feature or loose item
// under 600px, a search that lives in the address, and the desktop table with its Feature column pinned in between. The
// touch rules are checked on this task's regions only (toolbar and list).
[TestFixture]
public class RetroFeaturesTouchTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task PhoneList_WhenOpened_ShouldShowOneRowLinkPerFeatureAndOpenTheItemRetro()
    {
        var arranged = await ArrangeAsync();
        var (features, touch) = await OpenAsync(string.Empty);

        await Expect(touch.PhoneList).ToBeVisibleAsync();
        await Expect(touch.LegendItems).ToHaveCountAsync(4);
        await Expect(touch.LegendItems).ToHaveTextAsync(["Went well", "Not in the plan", "Went badly", "Can be improved"]);
        await Expect(touch.TableBox).ToHaveCountAsync(0);

        var feature = features.RowOf(arranged.FeatureTitle);
        await Expect(feature).ToHaveCountAsync(1);
        await Expect(feature).ToContainTextAsync(arranged.FeatureKey);
        await Expect(feature).ToContainTextAsync("New");
        await Expect(feature).ToContainTextAsync("to decide");
        await Expect(RetroFeaturesTouch.ChipOf(feature)).ToHaveTextAsync("No Feature retro yet");
        await Expect(RetroFeaturesTouch.MetaOf(feature)).ToHaveTextAsync(new Regex(@"^\d+ retros? · \d+ action items?$"));
        var loose = features.RowOf(arranged.LooseTitle);
        await Expect(features.LooseHeading).ToBeVisibleAsync();
        await Expect(loose).ToContainTextAsync(arranged.LooseKey);
        await Expect(RetroFeaturesTouch.MetaOf(loose)).ToHaveTextAsync("1 retro · 0 action items");
        await Expect(RetroFeaturesPage.CountOf(loose, "improve")).ToHaveAttributeAsync("data-count", "1");
        (await TouchRules.ViolationsAsync(Page, touch.ListHead)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, loose)).ShouldBeEmpty();
        (await loose.BoundingBoxAsync())!.Height.ShouldBeGreaterThanOrEqualTo(32);

        // The whole row is the link: a tap on its counts line opens the item's retros.
        await RetroFeaturesPage.CountOf(loose, "improve").TapAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"/retros/items/{arranged.LooseKey}$"));
        await new RetroItemPage(Page).Panel.WaitForAsync();
    }

    [Test]
    public async Task PhoneSearch_WhenOpenedAndCancelled_ShouldFilterThenClearTheAddress()
    {
        var arranged = await ArrangeAsync();
        var (features, touch) = await OpenAsync(string.Empty);
        (await TouchRules.ViolationsAsync(Page, features.Filter)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, touch.SearchOpen)).ShouldBeEmpty();

        await touch.SearchOpen.TapAsync();
        await Expect(features.Search).ToBeVisibleAsync();
        (await features.Search.EvaluateAsync<string>("e => getComputedStyle(e).fontSize")).ShouldBe("16px");
        await features.Search.FillAsync(arranged.FeatureTitle);
        await Expect(Page).ToHaveURLAsync(new Regex("q="));
        await Expect(features.Rows).ToHaveCountAsync(1);
        await Expect(features.RowOf(arranged.LooseTitle)).ToHaveCountAsync(0);

        await features.Search.FillAsync(arranged.LooseTitle);
        await Expect(features.Rows).ToHaveCountAsync(1);
        await Expect(features.RowOf(arranged.LooseTitle)).ToHaveCountAsync(1);

        await features.Search.FillAsync($"no such feature {Guid.NewGuid():N}");
        await Expect(touch.Empty).ToContainTextAsync("No Feature matches");
        await Expect(touch.ClearSearch).ToBeVisibleAsync();

        await touch.SearchCancel.TapAsync();
        await Expect(features.Search).ToHaveCountAsync(0);
        Page.Url.ShouldNotContain("q=");
        await Expect(features.RowOf(arranged.FeatureTitle)).ToHaveCountAsync(1);
    }

    [Test]
    public async Task PhoneToDecide_WhenNothingLeftToDecide_ShouldSayNothingLeftToDecide()
    {
        var arranged = await ArrangeAsync(decided: true);
        var (features, touch) = await OpenAsync("?show=open");

        await Expect(touch.Empty).ToHaveTextAsync("Nothing left to decide.");
        await Expect(features.Rows).ToHaveCountAsync(0);
        await Expect(touch.ClearSearch).ToHaveCountAsync(0);
        await Expect(touch.Count).ToHaveTextAsync("0 Features with retros");

        await features.FilterOption("All").TapAsync();
        await Expect(features.RowOf(arranged.FeatureTitle)).ToHaveCountAsync(1);
    }

    [Test]
    public async Task Tablet_WhenOpenedAt768_ShouldKeepTheTableWithThePinnedFeatureColumn()
    {
        var arranged = await ArrangeAsync();
        await Page.SetViewportSizeAsync(768, 1024);
        var (features, touch) = await OpenAsync(string.Empty);

        await Expect(touch.TableBox).ToHaveCountAsync(1);
        await Expect(touch.PhoneList).ToHaveCountAsync(0);
        await Expect(touch.SearchOpen).ToHaveCountAsync(0);
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= document.documentElement.clientWidth")).ShouldBeTrue();

        var row = features.RowOf(arranged.FeatureTitle);
        (await row.BoundingBoxAsync())!.Height.ShouldBeGreaterThanOrEqualTo(48);
        var cell = RetroFeaturesTouch.FeatureCellOf(row);
        var before = (await cell.BoundingBoxAsync())!.X;
        await touch.TableBox.EvaluateAsync("e => { e.scrollLeft = 150; }");
        (await touch.TableBox.EvaluateAsync<double>("e => e.scrollLeft")).ShouldBeGreaterThan(0);
        (await cell.BoundingBoxAsync())!.X.ShouldBe(before, 1);
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= document.documentElement.clientWidth")).ShouldBeTrue();

        // The whole row opens the item's retros, not only the title.
        await RetroFeaturesPage.CountOf(row, "wentWell").TapAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"/retros/items/{arranged.FeatureKey}$"));
    }

    // What each test seeds is settled afterwards (its points archived), so no test leaves points to decide behind for the next.
    [TearDown]
    public async Task SettleSeededPointsAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            "UPDATE retro_point SET archived_at = now() WHERE archived_at IS NULL AND text LIKE ANY (@patterns)",
            new { patterns = new[] { "Went badly %", "Went well %", "Improve %" } });
    }

    private async Task<(RetroFeaturesPage Features, RetroFeaturesTouch Touch)> OpenAsync(string query)
    {
        await Page.GotoAsync(Links.RetroFeatures(Project) + query);
        var features = new RetroFeaturesPage(Page);
        await features.Panel.WaitForAsync();
        return (features, new RetroFeaturesTouch(Page));
    }

    private sealed record Arranged(string FeatureKey, string FeatureTitle, string LooseKey, string LooseTitle);

    // A Feature with no retro of its own (its story has one, with points to decide) and a loose story with one Improve point.
    private async Task<Arranged> ArrangeAsync(bool decided = false)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (featureTitle, looseTitle) = ($"Phone feature {suffix}", $"Phone loose story {suffix}");
        var featureId = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle(featureTitle).InsertAsync(connection);
        var storyId = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle($"Phone story {suffix}").WithParent(featureId).InsertAsync(connection);
        var looseId = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle(looseTitle).InsertAsync(connection);
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var storyRetro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: storyId, AgentName: "story-agent"));
        await seed.PointAsync(storyRetro, new RetroSeedPoint(RetroCategory.WentBadly, $"Went badly {suffix}", NotNeededReason: decided ? "A one-off" : null));
        await seed.PointAsync(storyRetro, new RetroSeedPoint(RetroCategory.WentWell, $"Went well {suffix}"));
        var looseRetro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: looseId, AgentName: "loose-agent"));
        await seed.PointAsync(looseRetro, new RetroSeedPoint(RetroCategory.Improve, $"Improve {suffix}", NotNeededReason: decided ? "A one-off" : null));
        return new Arranged(await KeyAsync(connection, featureId), featureTitle, await KeyAsync(connection, looseId), looseTitle);
    }

    private static async Task<string> KeyAsync(NpgsqlConnection connection, Guid id) =>
        (await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }))!;
}
