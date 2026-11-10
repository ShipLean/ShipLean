using ShipLean.Contracts.Asks;
using Shouldly;

namespace ShipLean.UnitTest.Dtos.Asks;

[TestFixture]
public class ItemAsksDtoTests
{
    [Test]
    public void ItemAsksDto_WhenStoryRoundTripped_ShouldMatchTheFixture()
    {
        AskDtoFixture.SurvivesRoundTrip<ItemAsksDto>(AskDtoFixture.Read("item-asks.story.json")).ShouldBeTrue();
    }

    [Test]
    public void ItemAsksDto_WhenFeatureRoundTripped_ShouldMatchTheFixture()
    {
        AskDtoFixture.SurvivesRoundTrip<ItemAsksDto>(AskDtoFixture.Read("item-asks.feature.json")).ShouldBeTrue();
    }

    // The story mirrors the design's waiting state: Q1 open, Q2 answered, D1–D4 open, D5 confirmed, D6 changed, D7 withdrawn.
    [Test]
    public void ItemAsksDto_WhenStoryRead_ShouldMirrorTheWaitingDesign()
    {
        var story = AskDtoFixture.Deserialize<ItemAsksDto>("item-asks.story.json");

        story.Questions.Select(q => (q.Label, q.State)).ShouldBe([("Q1", QuestionState.Open), ("Q2", QuestionState.Answered)]);
        story.Decisions.Select(d => d.State).ShouldBe(
        [
            DecisionState.Open, DecisionState.Open, DecisionState.Open, DecisionState.Open,
            DecisionState.Confirmed, DecisionState.Changed, DecisionState.Withdrawn,
        ]);
        (story.OpenQuestions, story.OpenDecisions).ShouldBe((1, 4));
    }

    [Test]
    public void ItemAsksDto_WhenFeatureRead_ShouldHaveAnUnassignedWaitingChild()
    {
        var feature = AskDtoFixture.Deserialize<ItemAsksDto>("item-asks.feature.json");

        feature.WaitingChildren.Count.ShouldBe(2);
        feature.WaitingChildren.Count(c => c.OwnerIsCreator).ShouldBe(1);
    }
}
