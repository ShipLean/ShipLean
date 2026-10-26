using ShipLean.UnitTest.Architecture.Mcp.Planning;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.UnitTest.Mcp;

[TestFixture]
public class McpServerInstructionsTests
{
    [Test]
    public void Instructions_ShouldPointAtPlanningGuideWorkPart()
    {
        McpServerServices.Instructions.ShouldContain("planning_guide");
        McpServerServices.Instructions.ShouldContain("part work");
    }

    [Test]
    public void Instructions_ShouldOnlyNameRegisteredTools()
    {
        var allowed = GuideScan.RegisteredNames();
        foreach (var token in new[] { "attach_markdown", "comment", "set_status", "create_feature", "planning_guide" })
        {
            McpServerServices.Instructions.ShouldContain(token);
            allowed.ShouldContain(token);
        }
    }
}
