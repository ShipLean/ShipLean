using System.Text.Json.Nodes;
using ShipLean.UnitTest.Dtos.Projects;

namespace ShipLean.UnitTest.Dtos.Prompts;

// C11: the pinned patch cases the update producer must reproduce and the client parses.
[TestFixture]
public class PromptPatchesFixtureTests
{
    private static readonly string[] Required = ["oneChangedLine", "twoHunksFarApart", "noTrailingNewline", "insertAtEnd"];

    [Test]
    public void Fixture_ShouldHaveEveryRequiredCase()
    {
        var cases = ProjectDtoFixture.Read("prompts", "prompt-patches.json")["cases"]!.AsArray().Select(c => c!).ToList();

        cases.Select(c => c["name"]!.GetValue<string>()).ShouldBe(Required, ignoreOrder: true);
        foreach (var fixtureCase in cases)
        {
            fixtureCase["from"]!.GetValue<string>().ShouldNotBe(fixtureCase["to"]!.GetValue<string>());
            fixtureCase["patch"]!.GetValue<string>().ShouldStartWith("@@");
        }
    }
}
