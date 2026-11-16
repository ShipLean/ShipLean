using Dapper;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Identity;
using ShipLean.TestSupport.Retros;

namespace ShipLean.AcceptanceTests.Lanes.Retros.ViewerZone;

// The retro page dates a card in the viewer's zone. The viewer is in Asia/Bangkok; the retro is written at
// ViewerZoneTestData.LateEveningUtc (20:00 UTC, so the next day in Bangkok).
[TestFixture]
public class RetroViewerZoneTests : BangkokViewerTest
{
    // Mutation: pass TimeZoneInfo.Utc instead of the viewer's zone for CreatedAtLabel in RetroCards.ToCard.
    [Test]
    public async Task Retro_WhenViewerIsInBangkok_ShouldDateACardByTheBangkokDay()
    {
        string key;
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Zone retro story").InsertAsync(connection);
            var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
            var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: story, AgentName: "zone-agent", CreatedAt: ViewerZoneTestData.LateEveningUtc));
            await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Written late in the evening, UTC"));
            key = (await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @story", new { story }))!;
        }

        var item = new RetroItemPage(Page);
        await item.OpenAsync("SHIP", key);

        await Expect(item.Writers).ToContainTextAsync(ExpectedLabels.Relative(ViewerZoneTestData.LateEveningUtc, ViewerZoneTestData.Bangkok));
    }
}
