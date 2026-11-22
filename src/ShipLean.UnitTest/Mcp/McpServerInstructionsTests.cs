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
        foreach (var token in new[] { "attach_markdown", "comment", "set_status", "create_feature", "planning_guide", "get_brief", "report_task", "update_progress" })
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

    [Test]
    public void Instructions_ShouldNameTheRetroAnalysisTools()
    {
        foreach (var tool in new[] { "export_retro_points", "record_retro_analysis", "get_retro_analysis" })
        {
            McpServerServices.Instructions.ShouldContain(tool);
            GuideScan.RegisteredNames().ShouldContain(tool);
        }

        McpServerServices.Instructions.ShouldContain("part retro_analysis");
    }

    [Test]
    public void McpServerInstructions_ShouldTellAgentsToSendCommits()
    {
        McpServerServices.Instructions.ShouldContain("When you stop work on a Task or Bug, send the commits you made for it: report_task's commits for a task of a plan, add_commits for anything else.");
        McpServerServices.Instructions.ShouldContain("Each commit is its repository, as `git remote get-url origin` prints it, and its full sha (`git log --format=%H origin/main..HEAD`, only your own commits on a shared branch).");
        McpServerServices.Instructions.ShouldContain("Commits go on Tasks and Bugs only, never a Story or Feature.");
        McpServerServices.Instructions.ShouldContain("When one comes back with open_code_comments, fix each one and answer it in answered_comments.");
        GuideScan.RegisteredNames().ShouldContain("add_commits");
    }
}
