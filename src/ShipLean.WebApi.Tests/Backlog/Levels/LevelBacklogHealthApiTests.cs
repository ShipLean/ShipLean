using ShipLean.Contracts.Screens;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Backlog.Stories;
using ShipLean.WebApi.Tests.Screens;

namespace ShipLean.WebApi.Tests.Backlog.Levels;

// What the Features and Epics backlogs add to their rows: a Feature's health and what waits on the caller. Arranged items are the
// signed-in user's (Joel's), who owns nothing with an open ask in the seed.
[TestFixture]
public class LevelBacklogHealthApiTests : ApiFixture
{
    private static readonly string Features = $"api/projects/{SeedIds.Projects.Ship}/backlogs/features?includeCompleted=true";
    private static readonly string FeaturesTree = $"{Features}&mode=tree";
    private static readonly string Epics = $"api/projects/{SeedIds.Projects.Ship}/backlogs/epics?includeCompleted=true";

    [TestCase(false)]
    [TestCase(true)]
    public async Task Features_WhenFeatureHasHealth_ShouldCarryItOnTheRow(bool tree)
    {
        var view = await Api.GetBacklogAsync(tree ? FeaturesTree : Features);

        view.Rows.Single(r => r.Id == SeedIds.Screens.AtRiskFeature && r.Kind == BacklogRowKind.Item).Health.ShouldBe(FeatureHealth.AtRisk);
        view.Rows.Single(r => r.Id == SeedIds.Screens.BlockedFeature && r.Kind == BacklogRowKind.Item).Health.ShouldBe(FeatureHealth.Blocked);
        view.Rows.Single(r => r.Key == "SHIP-101" && r.Kind == BacklogRowKind.Item).Health.ShouldBeNull();
        view.Rows.Where(r => r.Kind != BacklogRowKind.Item).ShouldAllBe(r => r.Health == null && r.NeedsYou == null);
    }

    [Test]
    public async Task Features_WhenStoriesWaitOnCaller_ShouldCarryNeedsYouOnTheFeature()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await NeedsYouArrangement.ItemAsync(connection, "feature", SeedIds.Users.Joel);
        var story = await NeedsYouArrangement.ItemAsync(connection, "story", SeedIds.Users.Joel, feature);
        var quiet = await NeedsYouArrangement.ItemAsync(connection, "feature", SeedIds.Users.Joel);
        await NeedsYouArrangement.AsksAsync(connection, story, questions: 2, decisions: 0);
        await NeedsYouArrangement.AsksAsync(connection, feature, questions: 0, decisions: 1);

        var view = await Api.GetBacklogAsync(Features);

        view.Rows.Single(r => r.Id == feature).NeedsYou.ShouldBe(new NeedsYouDto(2, 1, true));
        view.Rows.Single(r => r.Id == quiet).NeedsYou.ShouldBeNull();
    }

    [Test]
    public async Task Features_WhenSignedInAsSomeoneElse_ShouldHaveNoNeedsYou()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await NeedsYouArrangement.ItemAsync(connection, "feature", SeedIds.Users.Joel);
        await NeedsYouArrangement.AsksAsync(connection, feature, questions: 1, decisions: 1);

        var view = await Api.ActingAs(TestUser.Priya).GetBacklogAsync(Features);

        view.Rows.Single(r => r.Id == feature).NeedsYou.ShouldBeNull();
    }

    [Test]
    public async Task Epics_WhenListed_ShouldHaveNoHealth()
    {
        await using var connection = await OpenDatabaseAsync();
        var epic = await NeedsYouArrangement.ItemAsync(connection, "epic", SeedIds.Users.Joel);
        await NeedsYouArrangement.AsksAsync(connection, epic, questions: 1, decisions: 0);

        var view = await Api.GetBacklogAsync(Epics);

        view.Rows.ShouldNotBeEmpty();
        view.Rows.ShouldAllBe(r => r.Health == null);
        view.Rows.Single(r => r.Id == epic).NeedsYou.ShouldBe(new NeedsYouDto(1, 0, false));
    }
}
