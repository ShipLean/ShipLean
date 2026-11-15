using ShipLean.Application.Features.Inspection.Assembly;
using static ShipLean.UnitTest.Inspection.Assembly.AssemblyTestData;

namespace ShipLean.UnitTest.Inspection.Assembly;

[TestFixture]
public class WhatItDidTests
{
    private static readonly (string, string) PromptId = ("prompt.id", "p-1");

    private static RunAssembler Assembler => new();

    [Test]
    public void Assemble_WhenThePromptTextMentionsReportTask_ShouldNotShowItAsWhatItDid()
    {
        var run = Run(
            [Span("i", null, "claude_code.interaction", 0, 30, PromptId)],
            [Log(0, "claude_code.user_prompt", "i", "p-1", ("prompt", "call report_task: done · 9 commits when finished"))]);

        Assembler.Assemble(run).WhatItDid.ShouldBeNull();
    }

    [Test]
    public void Assemble_WhenReportTaskIsCalled_ShouldSayItsStatusAndCommitCount()
    {
        var run = Run(
            [
                Span("i", null, "claude_code.interaction", 0, 30, PromptId),
                Span("r", "i", "claude_code.tool", 10, 12, ("tool_name", "mcp__shiplean__report_task"), ("tool_parameters", "{\"item\":\"SHIP-1\",\"status\":\"done\",\"commits\":[\"a\",\"b\",\"c\"]}")),
            ],
            [Log(0, "claude_code.user_prompt", "i", "p-1", ("prompt", "report_task: partial"))]);

        Assembler.Assemble(run).WhatItDid.ShouldBe("report_task: done · 3 commits");
    }

    [Test]
    public void Assemble_WhenHandBackIsTheLastReport_ShouldSayItsStatus()
    {
        var run = Run(
            [
                Span("i", null, "claude_code.interaction", 0, 30, PromptId),
                Span("r", "i", "claude_code.tool", 10, 12, ("tool_name", "mcp__shiplean__report_task"), ("tool_parameters", "{\"item\":\"SHIP-1\",\"status\":\"done\"}")),
                Span("h", "i", "claude_code.tool", 20, 22, ("tool_name", "mcp__shiplean__hand_back"), ("tool_parameters", "{\"item\":\"SHIP-1\",\"status\":\"blocked\"}")),
            ],
            []);

        Assembler.Assemble(run).WhatItDid.ShouldBe("hand_back: blocked");
    }
}
