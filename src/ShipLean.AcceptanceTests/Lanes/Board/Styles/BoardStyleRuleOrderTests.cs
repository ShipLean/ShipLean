using System.Text.Json.Nodes;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.AcceptanceTests.Pages.BoardSettings;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Board.Styles;

// Its own class: it rewrites the stories board's style rules.
[TestFixture]
public class BoardStyleRuleOrderTests : SeededAcceptanceTest
{
    [Test]
    public async Task Board_WhenSeveralRulesMatch_ShouldApplyTheFirstEnabledOne()
    {
        await using var tom = await BoardSettingsTeammate.SignInAsTomAsync(Page, BaseUrl);
        await tom.SaveSectionAsync(SeedIds.Boards.ShipStories, "styleRules", new JsonArray(
            Rule("rule-off", "Off", enabled: false, "Blocked"),
            Rule("rule-first", "First", enabled: true, "Blocked"),
            Rule("rule-second", "Second", enabled: true, "Mobile")));

        var board = new BoardScreen(Page);
        await board.OpenAsync("SHIP", "stories");
        await Expect(board.Headers).ToHaveCountAsync(5);

        await Expect(board.Card("SHIP-129")).ToHaveAttributeAsync("data-rule", "rule-first");
    }

    private static JsonObject Rule(string id, string name, bool enabled, string tag) => new()
    {
        ["id"] = id,
        ["name"] = name,
        ["enabled"] = enabled,
        ["colour"] = "danger",
        ["bold"] = false,
        ["italic"] = false,
        ["underline"] = false,
        ["criteria"] = new JsonObject
        {
            ["match"] = "all",
            ["clauses"] = new JsonArray(new JsonObject { ["field"] = "tags", ["op"] = "contains", ["value"] = tag }),
        },
    };
}
