using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;

namespace ShipLean.AcceptanceTests.Lanes.Board.Styles;

// The seeded Storefront stories board has a "Blocked" tag rule (danger) and a danger "Blocked" tag colour.
[TestFixture]
public class BoardStyleRuleTests : SeededAcceptanceTest
{
    [Test]
    public async Task Board_WhenBlockedTag_ShouldApplyStyleRule()
    {
        var board = new BoardScreen(Page);
        await board.OpenAsync("SHIP", "stories");
        await Expect(board.Headers).ToHaveCountAsync(5);

        var blocked = board.Card("SHIP-129");
        await Expect(blocked).ToHaveAttributeAsync("data-rule", "rule-blocked");
        await Expect(BoardStyles.Tag(blocked, "Blocked")).ToHaveAttributeAsync("data-colour", "danger");
        await Expect(BoardStyles.Tag(blocked, "Mobile")).ToHaveAttributeAsync("data-colour", "info");

        // A card without a matching tag carries no rule.
        await Expect(board.Card("SHIP-144")).Not.ToHaveAttributeAsync("data-rule", "rule-blocked");
    }
}
