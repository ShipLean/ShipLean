using System.Net;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Boards.View;

// The SHIP Stories board as the seed lays it out. Nothing here writes.
[TestFixture]
public class BoardViewStoriesApiTests : ApiFixture
{
    private static readonly Guid Board = SeedIds.Boards.ShipStories;

    [Test]
    public async Task Board_WhenLoaded_ShouldResolveLanesByCriteria()
    {
        var board = await Api.BoardAsync(Board);

        board.Level.ShouldBe(Contracts.Boards.BoardLevel.Stories);
        board.Lanes.Select(l => l.Id).ShouldBe(["lane-expedite", "lane-default"]);
        var expedite = board.Lane("lane-expedite");
        expedite.Kind.ShouldBe(BoardLaneKind.Criteria);
        expedite.CriteriaText.ShouldBe("Priority = 1");
        expedite.AcceptsDrops.ShouldBeTrue();
        board.Lane("lane-default").Kind.ShouldBe(BoardLaneKind.Default);
        expedite.Cells.SelectMany(c => c.Cards).Select(c => c.Key).ShouldBe(["SHIP-160"]);
        expedite.Cell("col-ready", doneHalf: true).Cards.Select(c => c.Key).ShouldBe(["SHIP-160"]);
        board.Lane("lane-default").Cells.SelectMany(c => c.Cards).ShouldNotContain(c => c.Key == "SHIP-160");
        board.Cards().ShouldAllBe(c => c.Rollups.All(r => r.Type == WorkItemTypeId.Task));
    }

    [Test]
    public async Task Board_WhenSplitColumn_ShouldCellEachHalf()
    {
        var board = await Api.BoardAsync(Board);

        var ready = board.Columns.Single(c => c.Id == "col-ready");
        ready.Split.ShouldBeTrue();
        (ready.DoingCount + ready.DoneCount).ShouldBe(ready.Count);
        ready.DoneCount.ShouldBe(board.Lanes.Sum(l => l.Cell("col-ready", doneHalf: true).Cards.Count));
        board.Lane("lane-default").Cells.Count(c => c.ColumnId == "col-ready").ShouldBe(2);
        board.Columns.Single(c => c.Id == "col-new").States.Select(s => s.Type).ShouldBe([WorkItemTypeId.Story, WorkItemTypeId.Bug], ignoreOrder: true);
        ready.DefinitionOfDoneHtml.ShouldContain("<li>");
    }

    [Test]
    public async Task Board_WhenDoneHoldsMoreThanFour_ShouldShowFourThenCountTheRest()
    {
        var board = await Api.BoardAsync(Board);
        var done = board.Lane("lane-default").Cell("col-done");
        await using var connection = await OpenDatabaseAsync();
        var newestFirst = (await connection.QueryAsync<string>(
            "SELECT key FROM work_item WHERE board_column_id = 'col-done' AND project_id = @p AND type IN ('story', 'bug') ORDER BY state_changed_at DESC, id",
            new { p = SeedIds.Projects.Ship })).ToList();

        done.Cards.Select(c => c.Key).ShouldBe(newestFirst.Take(4));
        done.OlderCount.ShouldBe(newestFirst.Count - 4);
        done.OlderCursor.ShouldNotBeNull();

        var everything = await Api.BoardAsync(Board, $"?older={Uri.EscapeDataString(done.OlderCursor)}");

        var expanded = everything.Lane("lane-default").Cell("col-done");
        expanded.Cards.Select(c => c.Key).ShouldBe(newestFirst);
        expanded.OlderCount.ShouldBe(0);
        expanded.OlderCursor.ShouldBeNull();
    }

    [Test]
    public async Task Board_WhenSeveralPeopleGiven_ShouldShowCardsOfAnyOfThem()
    {
        var filtered = await Api.BoardAsync(Board, "?assignee=none&assignee=00000001-0000-4000-8000-000000000005");

        var keys = filtered.Cards().Select(c => c.Key).ToList();
        keys.ShouldContain("SHIP-151");
        keys.ShouldContain("SHIP-129");
        keys.Count.ShouldBeLessThan((await Api.BoardAsync(Board)).Cards().Count());
    }

    [Test]
    public async Task Board_WhenFiltered_ShouldHideCardsButKeepCounts()
    {
        var all = await Api.BoardAsync(Board);

        var filtered = await Api.BoardAsync(Board, "?q=passkey");

        filtered.Cards().Select(c => c.Key).ShouldBe(["SHIP-129"]);
        filtered.Columns.Select(c => c.Count).ShouldBe(all.Columns.Select(c => c.Count));
        filtered.Columns.Select(c => c.OverLimit).ShouldBe(all.Columns.Select(c => c.OverLimit));
        filtered.Columns.ShouldAllBe(c => c.HiddenCount == (c.Id == "col-in-progress" ? c.Count - 1 : c.Count));
        all.Columns.ShouldAllBe(c => c.HiddenCount == 0);
        filtered.OpenCount.ShouldBe(all.OpenCount);
    }

    [TestCase("assignee=none", "SHIP-151")]
    [TestCase("assignee=00000001-0000-4000-8000-000000000005", "SHIP-129")]
    [TestCase("sprint=none", "SHIP-156")]
    [TestCase("q=ship-170", "SHIP-170")]
    [TestCase("q=SSO&assignee=none", "SHIP-170")]
    public async Task Board_WhenFilterGiven_ShouldShowOnlyMatchingCards(string query, string expectedKey)
    {
        var filtered = await Api.BoardAsync(Board, "?" + query);

        filtered.Cards().Select(c => c.Key).ShouldContain(expectedKey);
        filtered.Cards().Count().ShouldBeLessThan((await Api.BoardAsync(Board)).Cards().Count());
    }

    [Test]
    public async Task Board_WhenLanesOff_ShouldHoldEveryCardInOneLane()
    {
        await using var connection = await OpenDatabaseAsync();
        var apiAsPriya = Api.ActingAs(TestUser.Priya);
        await apiAsPriya.SetLanePreferenceAsync(Board, "off");

        var board = await apiAsPriya.BoardAsync(Board);

        board.Lanes.Select(l => l.Id).ShouldBe(["lane-default"]);
        board.Cards().Select(c => c.Key).ShouldContain("SHIP-160");
        (await Api.BoardAsync(Board)).Lanes.Count.ShouldBe(2);
    }

    [Test]
    public async Task Board_WhenCardCarriesSettings_ShouldFormatItsFieldsAndStyle()
    {
        var board = await Api.BoardAsync(Board);

        var passkey = board.Cards().Single(c => c.Key == "SHIP-129");
        passkey.Type.Name.ShouldBe("User Story");
        passkey.Tags.Select(t => t.Name).ShouldBe(["Mobile", "Blocked"]);
        passkey.StyleRuleId.ShouldBe("rule-blocked");
        passkey.Parent!.Key.ShouldBe("SHIP-102");
        passkey.Assignee!.Name.ShouldNotBeNullOrEmpty();
        passkey.ExtraFields.Select(f => f.FieldId).ShouldBe(["priority", "sprint"]);
        passkey.ExtraFields[0].Text.ShouldBe("2");
        passkey.Rollups.ShouldBe([new RollupCountDto(WorkItemTypeId.Task, 2, 2)]);
    }

    [Test]
    public async Task Board_WhenNotMember_ShouldBeNotFound()
    {
        using var response = await Api.ActingAs(TestUser.Kai).GetAsync($"api/boards/{Board}");

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound);
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Board_WhenTaskboard_ShouldBeValidationError()
    {
        using var response = await Api.GetAsync($"api/boards/{SeedIds.Boards.ShipTaskboard}");

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Board_WhenFilterIdIsMalformed_ShouldBeValidationError()
    {
        using var response = await Api.GetAsync($"api/boards/{Board}?assignee=priya");

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }
}
