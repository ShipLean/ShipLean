using System.Text.RegularExpressions;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Journeys;

// By work item on a phone (SHIP-727 D4): one collapsed card per item with its counts, a tap opens its notes (empty
// categories hidden), its raw failure log collapsed and the link to the item's retro page.
[TestFixture]
public class SprintRetroPhoneItemsTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private static readonly Guid ProjectId = SeedIds.Projects.Ship;
    private static readonly Guid RetroSprintId = SeedIds.Sprints.Sprint19;

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task SprintRetro_WhenWorkItemCardTappedOnPhone_ShouldOpenItsNotesLogAndRetroLink()
    {
        var (well, badly) = ($"Pairing cut the handover {Guid.NewGuid():N}", $"Reviews waited on one person {Guid.NewGuid():N}");
        var blocker = $"Staging was down {Guid.NewGuid():N}";
        string key;
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            var story = await new WorkItemBuilder(ProjectId).WithType("story").WithTitle($"Phone card story {Guid.NewGuid():N}").WithSprint(RetroSprintId).InsertAsync(connection);
            key = (await Dapper.SqlMapper.ExecuteScalarAsync<string>(connection, "SELECT key FROM work_item WHERE id = @story", new { story }))!;
            var seed = new RetroSeed(connection, ProjectId);
            var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: story, SprintId: RetroSprintId, AgentName: $"agent-{Guid.NewGuid():N}"));
            await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, well));
            await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, badly));
            await seed.FailureAsync(retro, RetroFailureKind.Blocker, blocker);
        }

        await Page.GotoAsync(Links.SprintRetro(Project, RetroSprintId, ("view", "item")));
        var retroPage = new SprintRetroPage(Page);
        await retroPage.Panel.WaitForAsync();

        var card = retroPage.Cards.Filter(new() { HasText = key });
        var toggle = card.GetByTestId(RetrosTestIds.RetroCardToggle);
        await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(card.GetByTestId(RetrosTestIds.RetroCardCount)).ToHaveCountAsync(4);
        await Expect(card.GetByTestId(RetrosTestIds.RetroCardToDecide)).ToHaveTextAsync("1 to decide");
        await Expect(card.GetByTestId(RetroSharedTestIds.RetroNote)).ToHaveCountAsync(0);
        (await TouchRules.ViolationsAsync(Page, toggle)).ShouldBeEmpty();

        await toggle.TapAsync();
        await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(card.GetByTestId(RetroSharedTestIds.RetroBoardColumn)).ToHaveCountAsync(2);
        await Expect(card.GetByTestId(RetroSharedTestIds.RetroNote).Filter(new() { HasText = well })).ToBeVisibleAsync();
        await Expect(card.GetByTestId(RetroSharedTestIds.RetroNote).Filter(new() { HasText = badly })).ToBeVisibleAsync();

        // The raw failure log is collapsed until tapped.
        await Expect(card.GetByTestId(RetroSharedTestIds.RetroCardLog)).ToHaveCountAsync(0);
        await card.GetByTestId(RetroSharedTestIds.RetroCardLogToggle).TapAsync();
        await Expect(card.GetByTestId(RetroSharedTestIds.RetroCardLog)).ToContainTextAsync(blocker);

        var link = card.GetByTestId(RetrosTestIds.RetroCardRetroLink);
        await Expect(link).ToHaveTextAsync($"Open {key} retro ›");
        (await TouchRules.ViolationsAsync(Page, card.GetByTestId(RetroSharedTestIds.RetroCardLogToggle))).ShouldBeEmpty();
        await link.TapAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"/retros/items/{key}"));
    }
}
