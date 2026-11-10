using ShipLean.Contracts.Screens;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Boards.View;

// Features carry their health on board cards; every other card type carries none. Nothing here writes.
[TestFixture]
public class BoardCardHealthApiTests : ApiFixture
{
    [Test]
    public async Task BoardView_WhenFeatureHasHealth_ShouldCarryItOnTheCard()
    {
        var board = await Api.BoardAsync(SeedIds.Boards.ShipFeatures);

        var cards = board.Cards().ToList();
        cards.Single(c => c.Key == "SHIP-102").Health.ShouldBe(FeatureHealth.AtRisk);
        cards.Single(c => c.Key == "SHIP-108").Health.ShouldBe(FeatureHealth.Blocked);
        cards.Single(c => c.Key == "SHIP-101").Health.ShouldBeNull();
    }

    [Test]
    public async Task BoardView_WhenStoryCard_ShouldHaveNoHealth()
    {
        var board = await Api.BoardAsync(SeedIds.Boards.ShipStories);

        board.Cards().ShouldNotBeEmpty();
        board.Cards().ShouldAllBe(c => c.Health == null);
    }
}
