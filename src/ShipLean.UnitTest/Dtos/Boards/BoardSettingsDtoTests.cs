using System.Reflection;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Persisted;

namespace ShipLean.UnitTest.Dtos.Boards;

[TestFixture]
public class BoardSettingsDtoTests
{
    [Test]
    public void BoardSettingsPutResult_WhenRoundTripped_ShouldMatchTheFixture()
    {
        BoardDtoFixture.SurvivesRoundTrip<BoardSettingsPutResult>(BoardDtoFixture.Read("boards", "board-settings-put-result.json")).ShouldBeTrue();
    }

    [Test]
    public void BoardSettingsPutResult_WhenMergedSectionsNamed_ShouldBeTopLevelPropertiesOfTheDocument()
    {
        var result = BoardDtoFixture.Read("boards", "board-settings-put-result.json");
        var sections = typeof(BoardSettingsJson).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..]);

        result["mergedSections"]!.AsArray().Select(s => s!.GetValue<string>()).ShouldBeSubsetOf(sections);
    }

    [TestCase("stories", "board-settings.stories.json")]
    [TestCase("features", "board-settings.features.json")]
    [TestCase("epics", "board-settings.epics.json")]
    [TestCase("taskboard", "board-settings.taskboard.json")]
    public void Seed_BoardSettingsJson_ShouldDeserialiseThroughTheDto(string level, string pinned)
    {
        var dto = new JsonObject
        {
            ["id"] = "00000007-0000-4000-8001-000000000001",
            ["projectId"] = "00000002-0000-4000-8000-000000000001",
            ["level"] = level,
            ["settings"] = BoardDtoFixture.ReadPinned(pinned),
            ["updatedAt"] = "2026-10-24T09:30:00+00:00",
            ["states"] = new JsonArray(),
            ["warnings"] = new JsonArray(),
        };

        BoardDtoFixture.SurvivesRoundTrip<BoardSettingsDto>(dto).ShouldBeTrue();
    }

    [Test]
    public void BoardSummaryDto_WhenRoundTripped_ShouldWriteTheLevelAsCamelCase()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new BoardSummaryDto(Guid.Empty, BoardLevel.Taskboard, "Taskboard"), BoardDtoFixture.Options);

        json.ShouldContain("\"level\":\"taskboard\"");
    }
}
