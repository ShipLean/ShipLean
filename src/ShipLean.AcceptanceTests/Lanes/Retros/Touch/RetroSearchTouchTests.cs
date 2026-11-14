using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.AcceptanceTests.Pages.Retros.Touch;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Touch;

// Retrospectives › Search on a phone (390×844, SHIP-730 D1, D3, D4): the search field alone, three-line rows, the Filter
// sheet that applies at once, the chips and No results. The open retro (L730b) is not asserted here. The touch rules are
// checked on this task's regions only (the toolbar and the list).
[TestFixture]
public class RetroSearchTouchTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Search_WhenTypingOnAPhone_ShouldShowThreeLineRowsAtFullWidth()
    {
        var arranged = await ArrangeAsync();
        var (search, touch) = await OpenAsync();

        await Expect(search.SearchBox).ToBeVisibleAsync();
        (await search.SearchBox.EvaluateAsync<string>("e => getComputedStyle(e).fontSize")).ShouldBe("16px");
        var viewport = await Page.EvaluateAsync<double>("document.documentElement.clientWidth");
        ((double)(await search.SearchBox.BoundingBoxAsync())!.Width).ShouldBeGreaterThanOrEqualTo(viewport * 0.9);

        await search.SearchBox.FillAsync(arranged.Suffix);
        await Expect(search.Row).ToHaveCountAsync(3);
        foreach (var row in await search.Row.AllAsync())
        {
            var box = (await row.BoundingBoxAsync())!;
            ((double)box.Width).ShouldBeGreaterThanOrEqualTo(viewport * 0.9);
            ((double)box.Height).ShouldBeGreaterThanOrEqualTo(32);
        }

        await Expect(RetroSearchTouch.RootCauseOf(search.Row)).ToHaveCountAsync(0);
        await search.SearchBox.FillAsync(arranged.CauseWord);
        await Expect(search.Row).ToHaveCountAsync(1);
        await Expect(RetroSearchTouch.RootCauseOf(search.Row)).ToContainTextAsync("Root cause:");
        await Expect(touch.Count).ToHaveTextAsync("1 point in 1 retro");

        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= document.documentElement.clientWidth")).ShouldBeTrue();
        (await TouchRules.ViolationsAsync(Page, search.Panel)).ShouldBeEmpty();
    }

    [Test]
    public async Task Filter_WhenSetInTheSheet_ShouldApplyAtOnceAndShowChips()
    {
        var arranged = await ArrangeAsync();
        var (search, touch) = await OpenAsync(("q", arranged.Suffix));
        await Expect(search.Row).ToHaveCountAsync(3);

        await touch.OpenSheetAsync();
        await touch.ChoiceOf("Went badly").TapAsync();
        await Expect(touch.Show).ToHaveTextAsync("Show 2 points");
        await Expect(Page).ToHaveURLAsync(new Regex("category=wentBadly"));
        await search.FilterUnthemed.TapAsync();
        await Expect(search.FilterUnthemed).ToHaveAttributeAsync("aria-checked", "true");
        await Expect(touch.Show).ToHaveTextAsync("Show 1 point");
        await Expect(Page).ToHaveURLAsync(new Regex("unthemed=true"));
        (await TouchRules.ViolationsAsync(Page, touch.Sheet)).ShouldBeEmpty();

        await touch.Show.TapAsync();
        await Expect(touch.Sheet).ToHaveCountAsync(0);
        await Expect(touch.Chips).ToHaveCountAsync(2);
        await Expect(touch.ChipsClear).ToBeVisibleAsync();
        await Expect(search.Row).ToHaveCountAsync(1);
        (await TouchRules.ViolationsAsync(Page, touch.ChipRow)).ShouldBeEmpty();

        await touch.ChipOf("Went badly").GetByTestId(RetrosTestIds.RetrosChipRemove).TapAsync();
        await Expect(touch.Chips).ToHaveCountAsync(1);
        Page.Url.ShouldNotContain("category=");
        Page.Url.ShouldContain("unthemed=true");
        await Expect(touch.ChipsClear).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Clear_WhenUsedInTheSheetOrOnTheChips_ShouldKeepTheSearchTextAndEmptyClearDropsIt()
    {
        var arranged = await ArrangeAsync();
        var (search, touch) = await OpenAsync(("q", arranged.Suffix), ("category", "wentBadly"), ("unthemed", "true"));
        await Expect(touch.FilterOpen).ToHaveAttributeAsync("data-count", "2");

        await touch.ChipsClear.TapAsync();
        await Expect(touch.Chips).ToHaveCountAsync(0);
        await Expect(search.SearchBox).ToHaveValueAsync(arranged.Suffix);
        Page.Url.ShouldContain("q=");

        await Page.GotoAsync(Page.Url + "&category=wentBadly&unthemed=true");
        await Expect(touch.Chips).ToHaveCountAsync(2);
        await touch.OpenSheetAsync();
        await touch.SheetClear.TapAsync();
        await Expect(touch.Chips).ToHaveCountAsync(0);
        await Expect(search.SearchBox).ToHaveValueAsync(arranged.Suffix);
        Page.Url.ShouldContain("q=");
        Page.Url.ShouldNotContain("category=");
    }

    [Test]
    public async Task Search_WhenOpenedWithATheme_ShouldShowThatThemesChip()
    {
        var arranged = await ArrangeAsync();
        var (_, touch) = await OpenAsync(("theme", arranged.ThemeId.ToString()));

        await Expect(touch.Chips).ToHaveCountAsync(1);
        await Expect(touch.Chips).ToContainTextAsync(arranged.ThemeName);
        await Expect(touch.FilterOpen).ToHaveAttributeAsync("data-count", "1");
    }

    [Test]
    public async Task Row_WhenTapped_ShouldAddSelectedAndPointAndBackShouldRemoveThem()
    {
        var arranged = await ArrangeAsync();
        var (search, _) = await OpenAsync(("q", arranged.Suffix));
        await Expect(search.Row).ToHaveCountAsync(3);

        await search.RowOf("Search phone plain").First.TapAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("selected=.*point=|point=.*selected="));

        await Page.GoBackAsync();
        await Expect(Page).Not.ToHaveURLAsync(new Regex("selected="));
        Page.Url.ShouldNotContain("point=");
        Page.Url.ShouldContain("q=");
    }

    [Test]
    public async Task Search_WhenNothingMatches_ShouldOfferClearSearchAndFilters()
    {
        await ArrangeAsync();
        var missing = $"nomatch{Guid.NewGuid():N}";
        var (search, touch) = await OpenAsync(("q", missing), ("category", "wentWell"));

        await Expect(search.Empty).ToContainTextAsync(missing);
        (await TouchRules.ViolationsAsync(Page, search.Empty)).ShouldBeEmpty();
        await touch.EmptyClear.TapAsync();
        await Expect(search.Empty).ToHaveCountAsync(0);
        await Expect(search.SearchBox).ToHaveValueAsync(string.Empty);
        await Expect(touch.Chips).ToHaveCountAsync(0);
        Page.Url.ShouldNotContain("q=");
        Page.Url.ShouldNotContain("category=");
    }

    // What each test seeds is settled afterwards (its points archived), so no test leaves points to decide behind for the next.
    [TearDown]
    public async Task SettleSeededPointsAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            "UPDATE retro_point SET archived_at = now() WHERE archived_at IS NULL AND text LIKE 'Search phone %'");
    }

    private async Task<(RetrosSearchPage Search, RetroSearchTouch Touch)> OpenAsync(params (string Key, string? Value)[] filters)
    {
        var search = new RetrosSearchPage(Page);
        await search.OpenAsync(Project, filters);
        return (search, new RetroSearchTouch(Page));
    }

    private sealed record Arranged(string Suffix, string CauseWord, Guid ThemeId, string ThemeName);

    // Three points with one word in common (the suffix): a themed Went badly point, an unthemed Went badly point whose root
    // cause holds a word of its own, and an unthemed Went well point.
    private async Task<Arranged> ArrangeAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var suffix = $"q{Guid.NewGuid():N}"[..12];
        var causeWord = $"cause{Guid.NewGuid():N}"[..14];
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var themeName = $"Phone theme {Guid.NewGuid():N}"[..24];
        var theme = await seed.ThemeAsync(themeName);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, SprintId: SeedIds.Sprints.Sprint23, AgentName: "phone-search-agent"));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, $"Search phone themed {suffix}", ThemeId: theme));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, $"Search phone plain {suffix}", RootCause: $"The brief hid a {causeWord} detail."));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, $"Search phone good {suffix}"));
        return new Arranged(suffix, causeWord, theme, themeName);
    }
}
