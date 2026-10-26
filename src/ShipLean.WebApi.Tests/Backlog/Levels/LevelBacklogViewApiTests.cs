using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Backlog.Stories;

namespace ShipLean.WebApi.Tests.Backlog.Levels;

// A completed Feature (SHIP-104), tags on SHIP-90, SHIP-101 and SHIP-103, and Spike coloured on the Epics board only: only this class
// changes the seed that way.
[TestFixture]
public class LevelBacklogViewApiTests : ApiFixture
{
    private static readonly string Root = $"api/projects/{SeedIds.Projects.Ship}/backlogs";

    private const string SpikeFilter = """{"match":"all","clauses":[{"field":"tags","op":"contains","value":"Spike"}]}""";

    private const string ThirdFilter = """{"match":"all","clauses":[{"field":"tags","op":"contains","value":"Third"}]}""";

    [OneTimeSetUp]
    public async Task Arrange()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE work_item SET state_id = (SELECT s.id FROM state s WHERE s.project_id = work_item.project_id AND s.type = 'feature'
                                             AND s.category = 'completed' AND s.archived_at IS NULL LIMIT 1)
            WHERE key = 'SHIP-104';
            UPDATE work_item SET tags = ARRAY['Spike'] WHERE key IN ('SHIP-90', 'SHIP-101');
            UPDATE work_item SET tags = ARRAY['Third'] WHERE key = 'SHIP-103';
            UPDATE board SET settings = jsonb_set(settings, '{tagColours}', '[{"tag":"Spike","colour":"warn"}]'::jsonb)
            WHERE level = 'epics' AND project_id = (SELECT id FROM project WHERE key = 'SHIP');
            """;
        (await command.ExecuteNonQueryAsync()).ShouldBe(5);
    }

    [Test]
    public async Task LevelBacklog_WhenCompletedHidden_ShouldOmitCompletedFeatures()
    {
        var hidden = await Api.GetBacklogAsync($"{Root}/features");
        var shown = await Api.GetBacklogAsync($"{Root}/features?includeCompleted=true");

        hidden.Rows.ShouldNotContain(row => row.Key == "SHIP-104");
        shown.Rows.ShouldContain(row => row.Key == "SHIP-104");
        hidden.TotalCount.ShouldBe(hidden.Rows.Count);
        shown.TotalCount.ShouldBe(hidden.TotalCount + 1);
    }

    [Test]
    public async Task LevelBacklog_WhenFilteredToALaterItem_ShouldKeepItsPlaceInTheWholeLevel()
    {
        var filtered = await Api.GetBacklogAsync($"{Root}/features?filter={Uri.EscapeDataString(ThirdFilter)}");

        filtered.Rows.Select(row => (row.Key, row.Rank)).ShouldBe([("SHIP-103", (int?)3)]);
    }

    [Test]
    public async Task LevelBacklog_WhenFiltered_ShouldKeepFullLevelRank()
    {
        var all = await Api.GetBacklogAsync($"{Root}/features?includeCompleted=true");
        var filtered = await Api.GetBacklogAsync($"{Root}/features?includeCompleted=true&filter={Uri.EscapeDataString(SpikeFilter)}");

        filtered.Rows.Select(row => row.Key).ShouldBe(["SHIP-101"]);
        filtered.Rows[0].Rank.ShouldBe(all.Rows.Single(row => row.Key == "SHIP-101").Rank);
        (await Api.GetBacklogAsync($"{Root}/features?includeCompleted=true&filter={Uri.EscapeDataString(SpikeFilter.Replace("Spike", "Nothing"))}"))
            .Rows.ShouldBeEmpty();
    }

    [Test]
    public async Task LevelBacklog_WhenTagged_ShouldUseTheLevelsOwnBoardColours()
    {
        var epics = await Api.GetBacklogAsync($"{Root}/epics");
        var features = await Api.GetBacklogAsync($"{Root}/features");

        epics.TagColours.ShouldBe([new TagColourJson("Spike", Swatch.Warn)]);
        epics.Rows.Single(row => row.Key == "SHIP-90").Tags.ShouldBe([new CardTagDto("Spike", Swatch.Warn)]);
        features.TagColours.ShouldNotContain(colour => colour.Tag == "Spike" && colour.Colour == Swatch.Warn);
        features.Rows.Single(row => row.Key == "SHIP-101").Tags.ShouldBe([new CardTagDto("Spike", features.TagColours.FirstOrDefault(c => c.Tag == "Spike")?.Colour)]);
        features.Level.ShouldBe(BoardLevel.Features);
    }
}
