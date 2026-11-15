using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Inspection;
using ShipLean.TestSupport;
using ShipLean.UnitTest.Dtos;

namespace ShipLean.UnitTest.Inspection;

// Each K5 DTO against its pinned JSON in ShipLean.TestSupport/Inspection/Shapes: nothing omitted, nulls explicit, camelCase.
[TestFixture]
public class InspectionShapeTests
{
    private static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    [TestCase("agent-runs-section.json", typeof(AgentRunsSectionDto))]
    [TestCase("run-page.json", typeof(RunPageDto))]
    [TestCase("run-row-detail.json", typeof(RunRowDetailDto))]
    [TestCase("inspection-project-settings.json", typeof(InspectionProjectSettingsDto))]
    [TestCase("agent-setup.json", typeof(AgentSetupDto))]
    [TestCase("inspection-usage.json", typeof(InspectionUsageDto))]
    [TestCase("inspection-health.json", typeof(InspectionHealthDto))]
    public void RoundTrip_WhenEachPinnedShape_ShouldMatch(string file, Type type)
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "Inspection", "Shapes", file)))!;

        var value = document.Deserialize(type, Options);

        JsonNode.DeepEquals(document, JsonSerializer.SerializeToNode(value, type, Options)).ShouldBeTrue(file);
    }

    [Test]
    public void Enums_WhenSerialized_ShouldBeCamelCase()
    {
        JsonSerializer.Serialize(LinkSource.FromToolCall, Options).ShouldBe("\"fromToolCall\"");
        JsonSerializer.Serialize(RowKind.ModelCall, Options).ShouldBe("\"modelCall\"");
        JsonSerializer.Serialize(InspectionUsageAction.SectionSeen, Options).ShouldBe("\"sectionSeen\"");
    }
}
