using ShipLean.Application.Features.Inspection.Assembly;
using static ShipLean.UnitTest.Inspection.Assembly.AssemblyTestData;

namespace ShipLean.UnitTest.Inspection.Assembly;

[TestFixture]
public class LogJoinTests
{
    private static readonly (string, string) PromptId = ("prompt.id", "p-1");

    private static string? PromptTextOf(params ShipLean.Contracts.Inspection.LogRecordData[] logs)
    {
        var run = Run([Span("i", null, "claude_code.interaction", 0, 30, PromptId)], logs);
        return new RunAssembler().Assemble(run).Turns.Single().PromptText;
    }

    [Test]
    public void Assemble_WhenALogHasNoSpanIdButTheInteractionsPromptIdAndTime_ShouldJoinTheInteraction()
    {
        PromptTextOf(Log(5, "claude_code.user_prompt", null, "p-1", ("prompt", "do it"))).ShouldBe("do it");
    }

    [Test]
    public void Assemble_WhenALogHasNoSpanIdAndItsTimeIsAfterTheInteraction_ShouldNotJoinIt()
    {
        PromptTextOf(Log(45, "claude_code.user_prompt", null, "p-1", ("prompt", "late"))).ShouldBeNull();
    }

    [Test]
    public void Assemble_WhenALogHasNoSpanIdAndAnotherPromptId_ShouldNotJoinIt()
    {
        PromptTextOf(Log(5, "claude_code.user_prompt", null, "p-2", ("prompt", "other"))).ShouldBeNull();
    }

    [Test]
    public void Assemble_WhenALogHasASpanIdOfARow_ShouldAttachToThatRowOnly()
    {
        var run = Run(
            [Span("i", null, "claude_code.interaction", 0, 30, PromptId), Span("t", "i", "claude_code.tool", 2, 4, ("tool_name", "Bash"))],
            [Log(4, "claude_code.tool_result", "t", "p-1", ("output", "ok"))]);

        var row = new RunAssembler().Assemble(run).Turns.Single().Rows.Single();

        (row.Logs.Count, row.Output).ShouldBe((1, "ok"));
    }
}
