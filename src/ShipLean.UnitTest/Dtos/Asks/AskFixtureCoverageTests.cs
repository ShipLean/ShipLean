using ShipLean.Contracts.Asks;
using ShipLean.Contracts.WorkItems;
using Shouldly;

namespace ShipLean.UnitTest.Dtos.Asks;

// Between them the asks fixtures carry every value of every wire enum, so a TS union or a C# member that drifts from the
// other side fails a round trip. DecisionAnswerAction is in-process only, and AskTagKind is pinned by the comments fixture.
[TestFixture]
public class AskFixtureCoverageTests
{
    [Test]
    public void AskFixtures_ShouldCoverEveryWireEnumValue()
    {
        ItemAsksDto[] items =
        [
            AskDtoFixture.Deserialize<ItemAsksDto>("item-asks.story.json"),
            AskDtoFixture.Deserialize<ItemAsksDto>("item-asks.feature.json"),
        ];
        var decisions = items.SelectMany(i => i.Decisions).ToList();
        var events = decisions.SelectMany(d => d.History).ToList();
        var lines = AskDtoFixture.Deserialize<HistoryDto>("history-asks.json").Asks;

        items.SelectMany(i => i.Questions).Select(q => q.State).Distinct().Order().ShouldBe(Enum.GetValues<QuestionState>());
        decisions.Select(d => d.State).Distinct().Order().ShouldBe(Enum.GetValues<DecisionState>());
        decisions.Select(d => d.AnswerKind).Concat(events.Select(e => e.ToKind).OfType<AnswerKind>()).Distinct().Order()
            .ShouldBe(Enum.GetValues<AnswerKind>());
        lines.Select(l => l.Kind).Concat(events.Select(e => e.Kind)).Distinct().Order().ShouldBe(Enum.GetValues<AskEventKind>());
    }
}
