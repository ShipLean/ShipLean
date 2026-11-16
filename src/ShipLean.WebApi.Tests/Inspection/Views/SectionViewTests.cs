using ShipLean.Contracts.Inspection;
using ShipLean.WebApi.Tests.Taskboard;

namespace ShipLean.WebApi.Tests.Inspection.Views;

[TestFixture]
public class SectionViewTests : InspectionViewsFixture
{
    [Test]
    public async Task Section_WhenARunIsLinked_ShouldSummariseItWithATimeline()
    {
        using var saved = await SetContentAsync(false);
        saved.EnsureSuccessStatusCode();

        var section = await Api.GetAsync<AgentRunsSectionDto>(RunsUrl);

        var row = section.Rows.ShouldHaveSingleItem();
        row.WhatItDid.ShouldBe("report_task: done \u00b7 1 commit");
        row.StartedLabel.ShouldBe("1m ago");
        row.AgentLabel.ShouldBe("Claude Code");
        row.LinkSource.ShouldBe(LinkSource.Tagged);
        row.Turns.ShouldBe(2);
        row.Errors.ShouldBe(4);
        row.Timeline.Select(s => s.Kind).Distinct().Order().ShouldBe(["error", "modelCall", "shipleanTool", "subagent", "tool", "wait"]);
        row.Timeline.ShouldAllBe(s => s.StartPct >= 0 && s.EndPct <= 100 && s.StartPct <= s.EndPct);
        section.Totals.ShouldBe(new AgentRunsTotalsDto(1, 85000, row.TokensIn, row.TokensOut, 4, 3000));
        section.ContentShown.ShouldBeFalse();
        section.Store.RetentionDays.ShouldBe(30);
    }

    [Test]
    public async Task RowDetail_WhenContentIsShown_ShouldCapOutputAtFortyLines()
    {
        using var saved = await SetContentAsync(true);
        saved.EnsureSuccessStatusCode();
        var edit = (await Api.GetAsync<RunPageDto>($"{RunsUrl}/{RunId}")).Turns[0].Rows.First(r => r.Name == "Edit");

        var detail = await Api.GetAsync<RunRowDetailDto>($"{RunsUrl}/{RunId}/rows/{edit.Id}");

        detail.Output.ShouldBe("edited Foo.cs");
        detail.OutputTruncated.ShouldBeFalse();
        detail.WaitMs.ShouldBe(3000);
        detail.Logs.ShouldNotBeEmpty();
        detail.StartLabel.ShouldMatch(@"^\d\d:\d\d:\d\d$");
    }
}
