using ShipLean.Contracts.Asks;
using Shouldly;

namespace ShipLean.UnitTest.Dtos.Asks;

[TestFixture]
public class WaitingDtoTests
{
    [Test]
    public void WaitingDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        AskDtoFixture.SurvivesRoundTrip<WaitingDto>(AskDtoFixture.Read("waiting.json")).ShouldBeTrue();
    }

    [Test]
    public void WaitingDto_WhenRead_ShouldListOldestOpenAskFirst()
    {
        var waiting = AskDtoFixture.Deserialize<WaitingDto>("waiting.json");

        waiting.Items.Select(i => i.OldestAt).ShouldBeInOrder(SortDirection.Ascending);
        (waiting.OpenQuestions, waiting.OpenDecisions).ShouldBe((waiting.Items.Sum(i => i.OpenQuestions), waiting.Items.Sum(i => i.OpenDecisions)));
    }
}
