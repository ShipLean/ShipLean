using ShipLean.Contracts.Prompts;

namespace ShipLean.UnitTest.Prompts;

// The wire values PromptKeys gives a version's source (SHIP-2231 C1): stored in prompt_version.source and sent to the client.
[TestFixture]
public class PromptKeysTests
{
    [Test]
    public void ToWire_WhenMcp_ShouldBeMcp()
    {
        var wire = PromptKeys.ToWire(PromptVersionSource.Mcp);

        wire.ShouldBe("mcp");
    }

    [Test]
    public void ToWire_WhenEachSource_ShouldBeTheSchemasValues()
    {
        var wires = Enum.GetValues<PromptVersionSource>().Select(PromptKeys.ToWire);

        wires.ShouldBe(["editor", "builtInUpdate", "migrated", "mcp"]);
    }
}
