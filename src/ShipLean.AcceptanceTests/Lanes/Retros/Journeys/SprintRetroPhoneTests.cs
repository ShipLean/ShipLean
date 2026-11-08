using System.Text.RegularExpressions;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Journeys;

// The sprint Retro tab on a phone (SHIP-727 D1-D3, D7): the category tiles pick the one column on screen and are kept in
// the address as `cat`, Points to decide turns To decide on and moves off Went well, and the Retros number opens
// Coverage as a sheet. The touch rules are checked on this task's regions only (numbers, toolbar, tiles).
[TestFixture]
public class SprintRetroPhoneTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private static readonly Guid ProjectId = SeedIds.Projects.Ship;
    private static readonly Guid RetroSprintId = SeedIds.Sprints.Sprint19;

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task SprintRetro_WhenCategoryTilePickedOnPhone_ShouldShowOnlyThatCategoryAndKeepItInAddress()
    {
        var (well, badly) = ($"Pairing cut the handover {Guid.NewGuid():N}", $"Reviews waited on one person {Guid.NewGuid():N}");
        await SeedAsync(new RetroSeedPoint(RetroCategory.WentWell, well), new RetroSeedPoint(RetroCategory.WentBadly, badly));
        var (retro, phone) = await OpenAsync();

        // Opens on Went well: its tile pressed, its column the only one, no cat in the address.
        await Expect(phone.Tile("Went well")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(retro.Columns).ToHaveCountAsync(1);
        await Expect(retro.Columns).ToHaveAttributeAsync("data-category", "wentWell");
        await Expect(retro.PointOf(well)).ToBeVisibleAsync();
        await Expect(retro.PointOf(badly)).ToHaveCountAsync(0);
        Page.Url.ShouldNotContain("cat=");
        (await TouchRules.ViolationsAsync(Page, phone.Tiles)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, phone.Toolbar)).ShouldBeEmpty();

        await phone.Tile("Went badly").TapAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("cat=wentBadly"));
        await Expect(phone.Tile("Went badly")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(phone.Tile("Went well")).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(retro.Columns).ToHaveCountAsync(1);
        await Expect(retro.Columns).ToHaveAttributeAsync("data-category", "wentBadly");
        await Expect(retro.PointOf(badly)).ToBeVisibleAsync();
        await Expect(retro.PointOf(well)).ToHaveCountAsync(0);

        // The address keeps it: a reload opens on the same category.
        await Page.ReloadAsync();
        await Expect(retro.Columns).ToHaveAttributeAsync("data-category", "wentBadly");
        await Expect(retro.PointOf(badly)).ToBeVisibleAsync();
    }

    [Test]
    public async Task SprintRetro_WhenPointsToDecideTappedOnPhone_ShouldFilterAndMoveToFirstCategoryToDecide()
    {
        var (decided, badly) = ($"Standups ran long {Guid.NewGuid():N}", $"Reviews waited on one person {Guid.NewGuid():N}");
        // Not in the plan (straight after Went well) has only a decided point, so the first category to decide is Went badly.
        await SeedAsync(new RetroSeedPoint(RetroCategory.NotInPlan, decided, NotNeededReason: "A one-off"), new RetroSeedPoint(RetroCategory.WentBadly, badly));
        var (retro, phone) = await OpenAsync();
        await Expect(phone.NumberToDecide).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(phone.Tile("Not in the plan")).ToContainTextAsync("nothing to decide");
        (await TouchRules.ViolationsAsync(Page, phone.Numbers)).ShouldBeEmpty();

        await phone.NumberToDecide.TapAsync();
        await Expect(phone.NumberToDecide).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Page).ToHaveURLAsync(new Regex("decide=open.*cat=wentBadly|cat=wentBadly.*decide=open"));
        await Expect(phone.Tile("Went badly")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(retro.Columns).ToHaveAttributeAsync("data-category", "wentBadly");
        await Expect(retro.PointOf(badly)).ToBeVisibleAsync();
        await Expect(phone.FilterOpen).ToHaveAttributeAsync("data-count", "1");

        // To decide hides the decided point; tapped again it is back, and the category stays.
        await phone.Tile("Not in the plan").TapAsync();
        await Expect(retro.PointOf(decided)).ToHaveCountAsync(0);
        await phone.NumberToDecide.TapAsync();
        await Expect(phone.NumberToDecide).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(retro.Columns).ToHaveAttributeAsync("data-category", "notInPlan");
        await Expect(retro.PointOf(decided)).ToBeVisibleAsync();
    }

    [Test]
    public async Task SprintRetro_WhenRetrosNumberTappedOnPhone_ShouldOpenCoverageSheet()
    {
        await SeedAsync(new RetroSeedPoint(RetroCategory.WentWell, $"Contracts first worked {Guid.NewGuid():N}"));
        var (retro, phone) = await OpenAsync();
        await Expect(retro.Rail).ToHaveCountAsync(0);
        await Expect(retro.Coverage).ToHaveCountAsync(0);

        await phone.NumberRetros.TapAsync();
        await Expect(phone.Sheet).ToBeVisibleAsync();
        await Expect(phone.SheetTitle).ToHaveTextAsync("Coverage");
        await Expect(phone.Sheet.GetByTestId(RetrosTestIds.RetroCoverage)).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, phone.Numbers)).ShouldBeEmpty();
    }

    private async Task<(SprintRetroPage Retro, SprintRetroPhone Phone)> OpenAsync()
    {
        await Page.GotoAsync(Links.SprintRetro(Project, RetroSprintId));
        var retro = new SprintRetroPage(Page);
        await retro.Panel.WaitForAsync();
        var phone = new SprintRetroPhone(Page);
        await phone.Numbers.WaitForAsync();
        return (retro, phone);
    }

    private async Task SeedAsync(params RetroSeedPoint[] points)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var seed = new RetroSeed(connection, ProjectId);
        var retroId = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, SprintId: RetroSprintId, AgentName: $"agent-{Guid.NewGuid():N}"));
        foreach (var point in points)
            await seed.PointAsync(retroId, point);
    }
}
