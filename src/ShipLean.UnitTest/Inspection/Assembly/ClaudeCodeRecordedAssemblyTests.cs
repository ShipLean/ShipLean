using System.Text.Json;
using ShipLean.Application.Features.Inspection.Assembly;
using ShipLean.Contracts.Inspection;
using ShipLean.TestSupport.Inspection;

namespace ShipLean.UnitTest.Inspection.Assembly;

// What the real Claude Code run (Recorded/claude-code-recorded) needs from the assembler that the synthetic fixtures did not: spans with no
// prompt.id, tool_result logs that point at the interaction span, and a failed command shown only by its tool.execution child.
[TestFixture]
public class ClaudeCodeRecordedAssemblyTests
{
    private static AssembledRun Assemble(Func<RawRun, RawRun>? change = null)
    {
        var raw = InspectionFixtures.Get("claude-code-recorded").Expected;
        return new RunAssembler().Assemble(change is null ? raw : change(raw));
    }

    private static RunRow Row(AssembledRun run, string name) => run.Turns.SelectMany(t => t.Rows).Single(r => r.Name == name);

    [Test]
    public void Assemble_WhenTheSpansHaveNoPromptId_ShouldTakeTheTurnsFromItsLogs()
    {
        var run = Assemble();

        run.Turns.Count.ShouldBe(1);
        run.Turns[0].PromptId.ShouldBe("prompt-0001");
    }

    [Test]
    public void Assemble_WhenAToolResultLogPointsAtTheInteraction_ShouldAttachItToTheToolRowByToolUseId()
    {
        var run = Assemble();

        Row(run, "Bash").Logs.Select(l => l.EventName).ShouldBe(["tool_decision", "tool_result"]);
        Row(run, "mcp__shiplean__pick_up").Kind.ShouldBe(RowKind.ShipleanTool);
    }

    [Test]
    public void Assemble_WhenOnlyTheExecutionChildFailed_ShouldMarkTheToolRowAsAnError()
    {
        var run = Assemble();

        Row(run, "Bash").IsError.ShouldBeTrue();
        Row(run, "ToolSearch").IsError.ShouldBeFalse();
        run.Markers.Count(m => m.Kind == MarkerKind.Error).ShouldBe(1);
    }

    [Test]
    public void Assemble_WhenAToolOutputSpanEventHoldsContent_ShouldNotCarryTheEventIntoTheRun()
    {
        var run = Assemble(raw => raw with
        {
            Spans = [.. raw.Spans.Select(s => s.Name == "claude_code.tool" && s.Attributes["tool_name"] == "Bash"
                ? s with { Events = [new SpanEventData(s.Start, "tool.output", new Dictionary<string, string> { ["output"] = "SECRET-TOOL-OUTPUT" })] }
                : s)],
        });

        JsonSerializer.Serialize(run, RawRunJson.Options).ShouldNotContain("SECRET-TOOL-OUTPUT");
    }
}
