using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Application.Features.Inspection.Assembly;
using ShipLean.TestSupport.Inspection;

namespace ShipLean.UnitTest.Inspection;

// Every synthetic expected.json assembles into its pinned <name>.assembled.json (the K4 rules, spelled out in Fixtures/README.md).
// Red until lane C1 replaces the stub RunAssembler; that is the only reason these fail.
[TestFixture]
[Category("RedUntilLaneC1")]
public class RunAssemblerContractTests
{
    [TestCase("claude-code-synthetic")]
    [TestCase("codex-cli-synthetic")]
    [TestCase("cursor-synthetic")]
    public void Assemble_WhenASyntheticRun_ShouldMatchItsAssembledJson(string name)
    {
        var fixture = InspectionFixtures.Get(name);
        var pinned = JsonNode.Parse(File.ReadAllText(fixture.AssembledPath))!;

        var assembled = new RunAssembler().Assemble(fixture.Expected);

        JsonNode.DeepEquals(JsonSerializer.SerializeToNode(assembled, RawRunJson.Options), pinned).ShouldBeTrue(name);
    }
}
