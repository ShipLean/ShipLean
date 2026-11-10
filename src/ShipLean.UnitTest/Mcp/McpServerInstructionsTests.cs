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
    public void Instructions_ShouldSendEveryBreakdownThroughThePlanningGuide()
    {
        McpServerServices.Instructions.ShouldContain("Before any breakdown of a feature or epic into stories and tasks, whatever its size");
        McpServerServices.Instructions.ShouldNotContain("for several agents, call planning_guide");
        foreach (var trigger in new[] { "break down", "breakdown", "plan this", "split into tasks", "capture_plan" })
        {
            McpServerServices.Instructions.ShouldContain(trigger);
        }
    }

    [Test]
    public void Instructions_ShouldDescribeTheResultShapeAndTheOnlyWayIn()
    {
        McpServerServices.Instructions.ShouldContain("Every result is one summary line");
        McpServerServices.Instructions.ShouldContain("only through these tools");
    }

    [Test]
    public void Instructions_ShouldOnlyNameRegisteredTools()
    {
        var allowed = GuideScan.RegisteredNames();
        foreach (var token in new[] { "attach_markdown", "comment", "set_status", "create_feature", "planning_guide", "get_brief", "report_task" })
        {
            McpServerServices.Instructions.ShouldContain(token);
            allowed.ShouldContain(token);
        }
    }

    [Test]
    public void Instructions_ShouldNameRecordDecisionsAndAsk()
    {
        McpServerServices.Instructions.ShouldContain("record_decisions");
        McpServerServices.Instructions.ShouldContain("with ask");
        McpServerServices.Instructions.ShouldContain("never wait");
    }
}
