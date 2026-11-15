using ShipLean.Application.Features.Inspection.Views;
using ShipLean.Contracts.Inspection;
using ShipLean.WebApi.Tests.Taskboard;

namespace ShipLean.WebApi.Tests.Inspection.Views;

[TestFixture]
public class RunPageFilterTests : InspectionViewsFixture
{
    private static RunPageRowDto Row(string id, string? parent, bool error)
        => new(id, parent, parent is null ? 0 : 1, RowKind.Tool, id, 0, 1, 1, error, false, null, null, 0, false);

    [Test]
    public void ErrorsWithAncestors_WhenAnErrorSitsUnderParents_ShouldKeepTheChainAndNothingElse()
    {
        var rows = new[] { Row("a", null, false), Row("b", "a", false), Row("c", "b", true), Row("d", "a", false), Row("e", null, false) };

        var kept = RunRowFilter.ErrorsWithAncestors(rows);

        kept.Select(r => r.Id).ShouldBe(["a", "b", "c"]);
    }

    [Test]
    public async Task Run_WhenErrorsOnly_ShouldKeepJustTheFailedRowsAndDropEmptyTurns()
    {
        var page = await Api.GetAsync<RunPageDto>($"{RunsUrl}/{RunId}?errorsOnly=true");

        page.Turns.Select(t => t.Number).ShouldBe([1]);
        page.Turns[0].Rows.Select(r => r.Name).ShouldBe(["claude_code.llm_request", "Bash"]);
        page.Turns[0].Rows.ShouldAllBe(r => r.IsError);
        page.Overview.Count.ShouldBe(2);
    }

    [Test]
    public async Task Run_WhenOneTurn_ShouldShowThatTurnAndZoomTheAxisToIt()
    {
        var all = await Api.GetAsync<RunPageDto>($"{RunsUrl}/{RunId}");
        var turn = await Api.GetAsync<RunPageDto>($"{RunsUrl}/{RunId}?turn=2");

        turn.Turns.Select(t => t.Number).ShouldBe([2]);
        turn.Axis.StartMs.ShouldBe(70000);
        turn.Axis.EndMs.ShouldBe(85000);
        all.Axis.ShouldBe(new RunAxisDto(0, 85000));
        turn.Overview.Select(o => (o.Turn, o.StartPct, o.EndPct, o.HasError, o.Waits.Count)).ShouldBe(all.Overview.Select(o => (o.Turn, o.StartPct, o.EndPct, o.HasError, o.Waits.Count)));
    }

    [Test]
    public async Task Run_WhenNoSuchTurn_ShouldBeAValidationError()
    {
        using var response = await Api.GetAsync($"{RunsUrl}/{RunId}?turn=9");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Run_WhenTheRunHasNeighbours_ShouldNameThemAndShowTheGrafanaLink()
    {
        var page = await Api.GetAsync<RunPageDto>($"{RunsUrl}/{RunId}");

        page.Header.OlderRunId.ShouldBeNull();
        page.Header.NewerRunId.ShouldBeNull();
        page.GrafanaUrl.ShouldBe("https://grafana.example.com/explore?traceId=00000000000000000000000000abc001");
        page.Header.WaitMs.ShouldBe(3000);
        page.Banner.ShouldBeNull();
    }
}
