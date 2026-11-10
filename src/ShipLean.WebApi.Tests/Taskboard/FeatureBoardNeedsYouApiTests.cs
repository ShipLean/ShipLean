using Dapper;
using ShipLean.Contracts.Screens;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Screens;

namespace ShipLean.WebApi.Tests.Taskboard;

// A Feature's board rows carry what waits on the caller for their story or bug.
[TestFixture]
public class FeatureBoardNeedsYouApiTests : ApiFixture
{
    [Test]
    public async Task FeatureBoard_WhenStoryHasOpenAsks_ShouldCarryNeedsYouOnTheRow()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await NeedsYouArrangement.ItemAsync(connection, "feature", SeedIds.Users.Joel);
        var waiting = await NeedsYouArrangement.ItemAsync(connection, "story", SeedIds.Users.Joel, feature);
        var bug = await NeedsYouArrangement.ItemAsync(connection, "bug", SeedIds.Users.Joel, feature);
        var quiet = await NeedsYouArrangement.ItemAsync(connection, "story", SeedIds.Users.Joel, feature);
        await NeedsYouArrangement.AsksAsync(connection, waiting, questions: 1, decisions: 1);
        await NeedsYouArrangement.AsksAsync(connection, bug, questions: 0, decisions: 2);
        await NeedsYouArrangement.AsksAsync(connection, feature, questions: 4, decisions: 0);
        var key = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature });
        var path = $"api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{key}/feature-board";

        var view = await Api.GetAsync<FeatureBoardViewDto>(path);
        var asSomeoneElse = await Api.ActingAs(TestUser.Priya).GetAsync<FeatureBoardViewDto>(path);

        view.Rows.Single(r => r.Parent.Id == waiting).NeedsYou.ShouldBe(new NeedsYouDto(1, 1, false));
        view.Rows.Single(r => r.Parent.Id == bug).NeedsYou.ShouldBe(new NeedsYouDto(0, 2, false));
        view.Rows.Single(r => r.Parent.Id == quiet).NeedsYou.ShouldBeNull();
        asSomeoneElse.Rows.ShouldAllBe(r => r.NeedsYou == null);
    }
}
