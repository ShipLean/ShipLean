using ShipLean.Application.Features.Inspection.Assembly;
using ShipLean.Contracts.Inspection;
using static ShipLean.UnitTest.Inspection.Assembly.AssemblyTestData;

namespace ShipLean.UnitTest.Inspection.Assembly;

[TestFixture]
public class RunAssemblerEdgeTests
{
    private static readonly (string, string) PromptId = ("prompt.id", "p-1");

    private static SpanRecord ErrorSpan(string id, int from, int to)
        => Span(id, "i", "claude_code.tool", from, to, ("tool_name", "Bash")) with { Status = SpanStatus.Error, StatusMessage = "boom" };

    private static RawRun DuplicatedRun(bool reversed)
    {
        SpanRecord[] spans =
        [
            Span("i", null, "claude_code.interaction", 0, 30, PromptId),
            Span("ok", "i", "claude_code.tool", 1, 2, ("tool_name", "Read")),
            ErrorSpan("bad", 3, 4),
            ErrorSpan("bad", 3, 4),
            Span("ok", "i", "claude_code.tool", 1, 2, ("tool_name", "Read")),
        ];
        return Run(reversed ? [.. spans.Reverse()] : spans, []);
    }

    [Test]
    public void Assemble_WhenAnErrorSpanIsExportedTwice_ShouldGiveOneRowAndOneErrorMarker()
    {
        var run = new RunAssembler().Assemble(DuplicatedRun(false));

        (run.Turns.Single().Rows.Count(row => row.Id == "bad"), run.Markers.Count(marker => marker.Kind == MarkerKind.Error)).ShouldBe((1, 1));
    }

    [Test]
    public void Assemble_WhenSpansAreDuplicatedInAnyInputOrder_ShouldGiveTheSameRun()
    {
        var forward = new RunAssembler().Assemble(DuplicatedRun(false));
        var backward = new RunAssembler().Assemble(DuplicatedRun(true));

        backward.Turns.Single().Rows.Select(row => row.Id).ShouldBe(forward.Turns.Single().Rows.Select(row => row.Id));
        backward.Markers.ShouldBe(forward.Markers);
    }

    [Test]
    public void Assemble_WhenALogIsExportedTwice_ShouldShowItOnce()
    {
        var log = Log(5, "claude_code.tool_result", "t", "p-1", ("output", "ok"));
        var run = Run([Span("i", null, "claude_code.interaction", 0, 30, PromptId), Span("t", "i", "claude_code.tool", 2, 6, ("tool_name", "Bash"))], [log, log]);

        new RunAssembler().Assemble(run).Turns.Single().Rows.Single().Logs.Count.ShouldBe(1);
    }

    [Test]
    public void Assemble_WhenANonAgentLogHasNoSpanAndFallsInsideATurn_ShouldShowItAsATopLevelEventRow()
    {
        var run = Run(
            [Span("i", null, "claude_code.interaction", 0, 30, PromptId)],
            [Log(10, "test_runner.finished", null, null, ("service.name", "dotnet-test"))]);

        var row = new RunAssembler().Assemble(run).Turns.Single().Rows.Single();

        (row.Kind, row.Name, row.ParentId, row.Depth).ShouldBe((RowKind.Event, "test_runner.finished", null, 0));
    }

    [Test]
    public void Assemble_WhenANonAgentLogFallsOutsideEveryTurn_ShouldNotShowIt()
    {
        var run = Run(
            [Span("i", null, "claude_code.interaction", 0, 30, PromptId)],
            [Log(50, "test_runner.finished", null, null)]);

        new RunAssembler().Assemble(run).Turns.Single().Rows.ShouldBeEmpty();
    }

    [Test]
    public void Assemble_WhenASpanParentIsMissingButItsPromptMatchesATurn_ShouldAttachItToThatTurnAsATopLevelRow()
    {
        var run = Run(
            [Span("i", null, "claude_code.interaction", 0, 30, PromptId), Span("orphan", "gone", "claude_code.tool", 5, 6, PromptId, ("tool_name", "Bash"))],
            []);

        var turn = new RunAssembler().Assemble(run).Turns.Single();

        (turn.Rows.Single().Id, turn.Rows.Single().ParentId, turn.Rows.Single().Depth).ShouldBe(("orphan", null, 0));
    }
}
