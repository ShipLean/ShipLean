using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Application.Features.Inspection.Assembly;
using ShipLean.Contracts.Inspection;
using ShipLean.TestSupport.Inspection;

namespace ShipLean.UnitTest.Inspection;

// Every synthetic expected.json assembles into its pinned <name>.assembled.json (the K4 rules, spelled out in Fixtures/README.md).
[TestFixture]
public class RunAssemblerContractTests
{
    [TestCase("claude-code-synthetic")]
    [TestCase("codex-cli-synthetic")]
    [TestCase("cursor-synthetic")]
    public void Assemble_WhenASyntheticRun_ShouldMatchItsAssembledJson(string name)
    {
        var fixture = InspectionFixtures.Get(name);
        var pinned = JsonSerializer.SerializeToNode(JsonSerializer.Deserialize<AssembledRun>(File.ReadAllText(fixture.AssembledPath), RawRunJson.Options), RawRunJson.Options);

        var assembled = new RunAssembler().Assemble(fixture.Expected);

        JsonNode.DeepEquals(JsonSerializer.SerializeToNode(assembled, RawRunJson.Options), pinned).ShouldBeTrue(name);
    }
}
