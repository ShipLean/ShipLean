using ShipLean.Contracts.WorkItems;
using Shouldly;

namespace ShipLean.UnitTest.Dtos.Asks;

[TestFixture]
public class AskHistoryDtoTests
{
    [Test]
    public void HistoryDto_WhenAsksRoundTripped_ShouldMatchTheFixture()
    {
        AskDtoFixture.SurvivesRoundTrip<HistoryDto>(AskDtoFixture.Read("history-asks.json")).ShouldBeTrue();
    }

    [Test]
    public void HistoryDto_WhenAsksRead_ShouldBeNewestFirst()
    {
        var history = AskDtoFixture.Deserialize<HistoryDto>("history-asks.json");

        history.Asks.Select(a => a.At).ShouldBeInOrder(SortDirection.Descending);
        history.Asks[^1].Badge.ShouldBe("D1–D7");
    }
}
