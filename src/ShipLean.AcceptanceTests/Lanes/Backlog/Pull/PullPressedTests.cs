using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.Application.Features.Mcp.Sprint;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems.Reads.Tree;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Pull;

// The pull line as the seeded Storefront project (SHIP) draws it, and Pull pressed without touching it. Its own class: it moves the items.
[TestFixture]
public class PullPressedTests : SeededAcceptanceTest
{
    [Test]
    public async Task Pull_WhenPressed_ShouldFillTheSprintToTheLine()
    {
        var planning = new PlanningBacklog(Page);
        var moves = new PlanningDragging(Page);
        var pull = new PullLine(Page);
        await Page.SetViewportSizeAsync(1280, 2400);
        await planning.OpenAsync("SHIP");
        await Expect(pull.Line).ToBeVisibleAsync();

        // The cut the server's rule makes over the real stack: the Backlog's points as the rows show them, Sprint 25's room and load.
        var pointsByKey = new Dictionary<string, decimal?>();
        foreach (var key in await moves.KeysInAsync("backlog"))
        {
            var text = await moves.Row(key).GetByTestId(BacklogTestIds.Points).InnerTextAsync();
            pointsByKey[key] = decimal.TryParse(text, out var points) ? points : null;
        }

        var load = await planning.Section("Sprint 25").InnerTextAsync();
        var figures = System.Text.RegularExpressions.Regex.Match(load, @"(\d+) / (\d+) pts");
        var alreadyIn = decimal.Parse(figures.Groups[1].Value);
        var room = decimal.Parse(figures.Groups[2].Value);
        var stack = pointsByKey.Select(p => new StackRowDto(
            Guid.NewGuid(), p.Key, p.Key, new TypeRef(WorkItemTypeId.Story, "User Story"),
            new StateRef(Guid.NewGuid(), "New", StateCategory.Proposed), p.Value, null, null, null, false, DateTimeOffset.UnixEpoch, [], 1)).ToList();
        var cut = SprintFill.Cut(stack, room, alreadyIn);
        var expected = cut.Selected.Select(r => r.Key).ToList();
        expected.ShouldNotBeEmpty();

        (await pull.KeysAboveAsync()).ShouldBe(expected);
        await Expect(pull.Sum).ToContainTextAsync($"{alreadyIn} + {cut.Selected.Sum(r => r.Points ?? 0)} = {cut.SprintPoints} / {room}");
        await Expect(pull.Sum).ToHaveAttributeAsync("data-over", "false");
        // The forecast's own line (drawn by the server's forecast) sits under the same row.
        (await planning.LineKeysAsync())[0].ShouldBe(expected[^1]);
        var sprintBefore = await moves.KeysInAsync(PullKeys.Sprint25);

        var response = await Page.RunAndWaitForResponseAsync(
            () => pull.Go.ClickAsync(),
            r => r.Request.Method == "POST" && r.Url.EndsWith("/backlog/bulk", StringComparison.Ordinal));

        response.Ok.ShouldBeTrue(await response.TextAsync());
        var items = PullKeys.ItemsOf(response.Request.PostData);
        items.Count.ShouldBe(expected.Count);
        // Rank is kept: the move sets the sprint and asks for no new place.
        items.ShouldAllBe(item => item.GetProperty("placement").ValueKind == JsonValueKind.Null);
        JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty("written").GetArrayLength().ShouldBe(expected.Count);
        await Expect(moves.Row(expected[0])).ToHaveAttributeAsync("data-section", PullKeys.Sprint25);

        await Page.ReloadAsync();
        await Expect(planning.Section("Backlog")).ToBeVisibleAsync();
        await Expect(moves.Row(expected[0])).ToBeVisibleAsync();
        // They arrive after the rows already there, in their Backlog order: their rank was not touched.
        (await moves.KeysInAsync(PullKeys.Sprint25)).ShouldBe([.. sprintBefore, .. expected]);
        (await moves.KeysInAsync("backlog")).ShouldNotContain(expected[0]);
    }
}
