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

// Merge themes, proposed merges, Unthemed and What keeps working on a phone (390×844, SHIP-731 D8): the full-screen merge
// opened from the address, a proposal with full-width answers, and the latest mention clamped to two lines. The touch
// rules are checked on this task's regions only. Every test seeds themes of its own, by name.
[TestFixture]
public class RetroInsightsMergeTouchTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Merge_WhenOpenedFromTheAddress_ShouldBeFullScreenAndDisabledUntilPicked()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var connection = await OpenAsync();
        var (from, into) = (await ThemeWithPointAsync(connection, $"Merge from {suffix}"), await ThemeWithPointAsync(connection, $"Merge into {suffix}"));
        var insights = new RetroInsightsPage(Page);
        var merge = new RetroMergeTouchPart(Page);

        await Page.GotoAsync(Links.RetroInsights(Project, ("view", "themes"), ("range", "w12")) + "&merge=");

        await Expect(merge.Dialog).ToBeVisibleAsync();
        await Expect(insights.MergeThemes).ToHaveCountAsync(0);
        var dialog = (await merge.Dialog.BoundingBoxAsync())!;
        ((double)dialog.Width).ShouldBe(390, 1);
        ((double)dialog.Height).ShouldBeGreaterThan(800);
        var close = (await merge.Close.BoundingBoxAsync())!;
        var primary = (await merge.Primary.BoundingBoxAsync())!;
        ((double)close.X).ShouldBeLessThan(60);
        ((double)primary.X).ShouldBeGreaterThan(280);
        await Expect(merge.Save).ToBeDisabledAsync();
        (await merge.Sources.EvaluateAllAsync<int>("boxes => boxes.filter(b => b.checked).length")).ShouldBe(0);
        (await merge.Target.EvaluateAsync<string>("e => getComputedStyle(e).fontSize")).ShouldBe("16px");
        (await TouchRules.ViolationsAsync(Page, merge.Dialog)).ShouldBeEmpty();

        await (await merge.SourceOfAsync(from)).CheckAsync();
        await Expect(merge.Save).ToBeDisabledAsync();
        await merge.Target.SelectOptionAsync(new SelectOptionValue { Value = into.ToString() });
        await Expect(merge.Save).ToBeEnabledAsync();
    }

    [Test]
    public async Task Merge_WhenOpenedWithATheme_ShouldTickIt()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var connection = await OpenAsync();
        var ticked = await ThemeWithPointAsync(connection, $"Tick me {suffix}");
        var other = await ThemeWithPointAsync(connection, $"Leave me {suffix}");
        var merge = new RetroMergeTouchPart(Page);

        await Page.GotoAsync(Links.RetroInsights(Project, ("view", "themes"), ("range", "w12")) + $"&merge={ticked}");

        await Expect(await merge.SourceOfAsync(ticked)).ToBeCheckedAsync();
        await Expect(await merge.SourceOfAsync(other)).Not.ToBeCheckedAsync();
    }

    [Test]
    public async Task Merge_WhenConfirmed_ShouldMergeAndClose()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (fromName, intoName) = ($"Fold away {suffix}", $"Keep this {suffix}");
        await using var connection = await OpenAsync();
        var from = await ThemeWithPointAsync(connection, fromName);
        var into = await ThemeWithPointAsync(connection, intoName);
        var merge = new RetroMergeTouchPart(Page);
        await Page.GotoAsync(Links.RetroInsights(Project, ("view", "themes"), ("range", "w12")));
        await Expect(merge.ThemeCards.Filter(new() { HasText = fromName })).ToHaveCountAsync(1);

        await Page.GotoAsync(Links.RetroInsights(Project, ("view", "themes"), ("range", "w12")) + "&merge=");
        await (await merge.SourceOfAsync(from)).CheckAsync();
        await merge.Target.SelectOptionAsync(new SelectOptionValue { Value = into.ToString() });
        await merge.Save.TapAsync();

        await Expect(merge.Dialog).ToHaveCountAsync(0);
        merge.AddressHasMerge.ShouldBeFalse();
        await Expect(merge.ThemeCards.Filter(new() { HasText = fromName })).ToHaveCountAsync(0);
        await Expect(merge.ThemeCards.Filter(new() { HasText = intoName })).ToHaveCountAsync(1);
    }

    [Test]
    public async Task MergeProposal_WhenShownOnAPhone_ShouldStackWithFullWidthButtons()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (fromName, intoName) = ($"Proposal from {suffix}", $"Proposal into {suffix}");
        await using var connection = await OpenAsync();
        var from = await ThemeWithPointAsync(connection, fromName);
        var into = await ThemeWithPointAsync(connection, intoName);
        await connection.ExecuteAsync(
            "INSERT INTO retro_theme_merge_proposal (id, project_id, from_theme_id, into_theme_id, reason, proposed_by_user_id) VALUES (@id, @project, @from, @into, 'Same thing.', @user)",
            new { id = Guid.NewGuid(), project = SeedIds.Projects.Ship, from, into, user = SeedIds.Users.Joel });
        var insights = new RetroInsightsPage(Page);

        await insights.OpenAsync(Project, "themes", "w12");

        var card = insights.ProposalOf(fromName);
        await Expect(card).ToHaveCountAsync(1);
        var box = (await card.BoundingBoxAsync())!;
        var keep = (await insights.ProposalKeep(fromName).BoundingBoxAsync())!;
        var answer = (await insights.ProposalMerge(fromName).BoundingBoxAsync())!;
        ((double)keep.Height).ShouldBeGreaterThanOrEqualTo(32);
        ((double)answer.Height).ShouldBeGreaterThanOrEqualTo(32);
        // Side by side, together spanning the card.
        Math.Abs((double)(keep.Y - answer.Y)).ShouldBeLessThan(2);
        ((double)(answer.X + answer.Width - keep.X)).ShouldBeGreaterThan((double)box.Width - 24);
        // Stacked: the answers sit under the two themes.
        ((double)keep.Y).ShouldBeGreaterThan((double)box.Y + 30);
        (await TouchRules.ViolationsAsync(Page, card)).ShouldBeEmpty();
    }

    [Test]
    public async Task KeepsWorking_WhenOnAPhone_ShouldClampTheLatestMentionToTwoLines()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var themeName = $"Works well {suffix}";
        var latest = string.Join(' ', Enumerable.Repeat($"Pairing before the merge kept the review short {suffix}.", 8));
        await using var connection = await OpenAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle($"Keeps working story {suffix}").InsertAsync(connection);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, WorkItemId: story));
        var theme = await seed.ThemeAsync(themeName, RetroTone.Good);
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, latest, ThemeId: theme, CreatedAt: TimeProvider.System.GetUtcNow().AddDays(-8)));
        var insights = new RetroInsightsPage(Page);
        var merge = new RetroMergeTouchPart(Page);

        await insights.OpenAsync(Project, "themes", "w12");

        var mention = merge.KeepsWorkingLatest.Filter(new() { HasText = suffix });
        await Expect(mention).ToHaveCountAsync(1);
        var clamp = await mention.EvaluateAsync<string>("e => getComputedStyle(e).webkitLineClamp");
        var lines = await mention.EvaluateAsync<double>("e => e.getBoundingClientRect().height / parseFloat(getComputedStyle(e).lineHeight)");
        clamp.ShouldBe("2");
        lines.ShouldBeLessThan(2.5);
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= document.documentElement.clientWidth")).ShouldBeTrue();
        (await TouchRules.ViolationsAsync(Page, insights.KeepsWorking)).ShouldBeEmpty();
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    // A theme with one Went badly point on a story with no sprint, eight days old, so a twelve-week range shows it.
    private static async Task<Guid> ThemeWithPointAsync(NpgsqlConnection connection, string name)
    {
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle($"Story for {name}").InsertAsync(connection);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, WorkItemId: story));
        var theme = await seed.ThemeAsync(name);
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, $"{name} point", ThemeId: theme, CreatedAt: TimeProvider.System.GetUtcNow().AddDays(-8)));
        return theme;
    }
}
