using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.AcceptanceTests.Pages.Taskboard;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Journeys.Planning;

// A new project goes from its first stories through one sprint to the sprint end, entirely through the screens.
[TestFixture]
public class NewProjectSprintJourneyTests : SeededAcceptanceTest
{
    private const string ProjectName = "Journey Planning";
    private const string ProjectKey = "JPL";

    // The tracks of a new project's Stories board and Task columns: New, Active, Resolved, Closed.
    private const int ActiveTrack = 1;
    private const int ClosedTrack = 3;

    [Test]
    public async Task J1_NewProjectSprint_RunsToSprintEnd()
    {
        // 1. Create the project.
        var form = new NewProjectPage(Page);
        await form.OpenAsync();
        await form.Name.FillAsync(ProjectName);
        await form.Key.FillAsync(ProjectKey);
        await Expect(form.KeyFree).ToHaveAttributeAsync("data-ok", "true");
        var created = await form.CreateAsync();
        created.Ok.ShouldBeTrue(await created.TextAsync());
        var projectId = (await created.JsonAsync())!.Value.GetProperty("id").GetGuid();
        await Expect(Page).ToHaveURLAsync(new Regex(Regex.Escape(Links.Backlog(ProjectKey)) + "$"));

        // 2. Three stories on the backlog, then their points on their own pages.
        var backlog = new StoriesBacklog(Page);
        await Expect(backlog.Toolbar).ToBeVisibleAsync();
        await Expect(backlog.Rows).ToHaveCountAsync(0);
        var stories = new List<(string Title, int Points, string Key, Guid Id)>();
        foreach (var (title, points) in new[] { ("Sign up", 2), ("Invite a teammate", 3), ("Export a report", 5) })
        {
            await backlog.NewItem.ClickAsync();
            await Expect(backlog.AddRow).ToBeVisibleAsync();
            var add = await backlog.AddAsync(title);
            add.Ok.ShouldBeTrue(await add.TextAsync());
            var item = (await add.JsonAsync())!.Value;
            stories.Add((title, points, item.GetProperty("key").GetString()!, item.GetProperty("id").GetGuid()));
            await Expect(backlog.Rows.Filter(new() { HasText = title })).ToHaveCountAsync(1);
        }

        var screen = new WorkItemScreen(Page);
        foreach (var story in stories)
        {
            await screen.OpenAsync(ProjectKey, story.Key);
            await screen.Points.FillAsync(story.Points.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var saved = await screen.SaveAsync(story.Id);
            saved.Ok.ShouldBeTrue(await saved.TextAsync());
        }

        // 3. Plan: a second sprint, and all three stories into Sprint 1.
        var planning = new PlanningBacklog(Page);
        var dragging = new PlanningDragging(Page);
        await dragging.ShowWholePageAsync();
        await planning.OpenAsync(ProjectKey);
        var createSprint = await Page.RunAndWaitForApiAsync(
            $"/api/projects/{projectId}/sprints",
            "POST",
            () => Page.GetByTestId(BacklogTestIds.PlanningCreateSprint).ClickAsync());
        createSprint.Ok.ShouldBeTrue(await createSprint.TextAsync());
        var sprint2 = (await createSprint.JsonAsync())!.Value.GetProperty("id").GetGuid().ToString();
        await Expect(planning.Section("Sprint 2")).ToBeVisibleAsync();
        foreach (var story in stories)
        {
            var moved = await dragging.DragOntoHeaderAsync(story.Key, "Sprint 1");
            moved.Ok.ShouldBeTrue(await moved.TextAsync());
            await Expect(dragging.Toasts.First).ToContainTextAsync($"{story.Key} → Sprint 1");
        }

        await Expect(planning.Committed("Sprint 1")).ToHaveTextAsync(new Regex(@"^10 / "));

        // 4. The 5-point story to Active on the Stories board: pulled by Joel, and it stays after a reload.
        var big = stories[2];
        var board = new BoardScreen(Page);
        var boardMoves = new BoardMoves(Page);
        await board.OpenAsync(ProjectKey, "stories");
        await Expect(board.Card(big.Key)).ToBeVisibleAsync();
        await Expect(BoardMoves.Assignee(board.Card(big.Key)).First).ToHaveAttributeAsync("data-variant", "none");
        var toActive = await boardMoves.DragToCellAsync(board.Card(big.Key), Cell(ActiveTrack));
        toActive.Ok.ShouldBeTrue(await toActive.TextAsync());
        await Expect(Cell(ActiveTrack).GetByTestId(BoardTestIds.Card).Filter(new() { HasText = big.Title })).ToHaveCountAsync(1);
        await Expect(board.CellOfTitled(big.Title)).ToHaveAttributeAsync("data-column", "col-active");
        await Expect(BoardMoves.Assignee(board.Card(big.Key)).First).ToHaveAttributeAsync("title", "Joel Dickson");
        await Page.ReloadAsync();
        await Expect(Cell(ActiveTrack).GetByTestId(BoardTestIds.Card).Filter(new() { HasText = big.Title })).ToHaveCountAsync(1);
        await Expect(board.CellOfTitled(big.Title)).ToHaveAttributeAsync("data-column", "col-active");
        await Expect(BoardMoves.Assignee(board.Card(big.Key)).First).ToHaveAttributeAsync("title", "Joel Dickson");

        // 5. The Taskboard: a task under the story, moved to its closed column; then the story to Closed on the board.
        var sprintsResponse = await Page.Context.APIRequest.GetAsync($"/api/projects/{projectId}/sprints");
        sprintsResponse.Ok.ShouldBeTrue(await sprintsResponse.TextAsync());
        var sprint1 = JsonDocument.Parse(await sprintsResponse.TextAsync()).RootElement.EnumerateArray()
            .Single(s => s.GetProperty("name").GetString() == "Sprint 1").GetProperty("id").GetGuid();

        var taskboard = new SprintTaskboard(Page);
        var taskMoves = new TaskboardMoves(Page);
        await taskboard.OpenAsync(ProjectKey, sprint1);
        var row = taskboard.RowTitled(big.Title);
        await row.GetByTestId(TaskboardTestIds.AddTask).ClickAsync();
        var task = await taskboard.AddTaskAsync("Build the export");
        task.Ok.ShouldBeTrue(await task.TextAsync());
        var taskCard = row.GetByTestId(TaskboardTestIds.Card).Filter(new() { HasText = "Build the export" });
        await Expect(taskCard).ToHaveCountAsync(1);
        var closedTask = await taskMoves.DragToCellAsync(taskCard, TaskboardMoves.Cell(row, ClosedTrack));
        closedTask.Ok.ShouldBeTrue(await closedTask.TextAsync());
        await Expect(TaskboardMoves.Cell(row, ClosedTrack).GetByTestId(TaskboardTestIds.Card).Filter(new() { HasText = "Build the export" })).ToHaveCountAsync(1);

        await board.OpenAsync(ProjectKey, "stories");
        await Expect(board.Card(big.Key)).ToBeVisibleAsync();
        var toClosed = await boardMoves.DragToCellAsync(board.Card(big.Key), Cell(ClosedTrack));
        toClosed.Ok.ShouldBeTrue(await toClosed.TextAsync());
        await Expect(Cell(ClosedTrack).GetByTestId(BoardTestIds.Card).Filter(new() { HasText = big.Title })).ToHaveCountAsync(1);
        await Expect(board.CellOfTitled(big.Title)).ToHaveAttributeAsync("data-column", "col-closed");

        // 6. Arrangement: Sprint 1 ran over the last fortnight, the sprint moves came before it and the state changes inside it.
        await ArrangeEndedSprintAsync(sprint1, stories.Select(s => s.Id).ToArray());

        // 7. The sprint-end prompt: the two unfinished stories roll into Sprint 2.
        var prompt = new SprintEndPrompt(Page);
        await prompt.OpenAsync(ProjectKey);
        await Expect(prompt.Rows).ToHaveCountAsync(2);
        foreach (var story in stories.Take(2))
        {
            await prompt.DestinationOf(story.Key).SelectOptionAsync("next");
        }

        var applied = await prompt.ApplyAsync(sprint1);
        applied.Ok.ShouldBeTrue(await applied.TextAsync());
        await Expect(prompt.Dialog).ToHaveCountAsync(0);

        // 8. All sprints reports Sprint 1; Planning shows the two stories in Sprint 2.
        var all = new AllSprints(Page);
        await all.OpenAsync(ProjectKey);
        var sprintRow = all.RowOf("Sprint 1");
        await Expect(sprintRow.GetByTestId(SprintsTestIds.RowCommitted)).ToHaveTextAsync("10");
        await Expect(sprintRow.GetByTestId(SprintsTestIds.RowCompleted)).ToHaveTextAsync("5");

        await planning.OpenAsync(ProjectKey);
        foreach (var story in stories.Take(2))
        {
            await Expect(dragging.Row(story.Key)).ToHaveAttributeAsync("data-section", sprint2);
        }
    }

    // A track of the board's one lane (a single lane shows no header, so it is found by position).
    private ILocator Cell(int track) => Page.GetByTestId(BoardTestIds.Lane).GetByTestId(BoardTestIds.Cell).Nth(track);

    // Moves Sprint 1 into the past and shifts the stories' history: sprint moves before the start date, everything else inside it.
    private async Task ArrangeEndedSprintAsync(Guid sprintId, Guid[] storyIds)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using (var dates = new NpgsqlCommand(
            "UPDATE sprint SET start_date = (now() AT TIME ZONE 'utc')::date - 14, end_date = (now() AT TIME ZONE 'utc')::date - 1 WHERE id = @id",
            connection))
        {
            dates.Parameters.AddWithValue("id", sprintId);
            (await dates.ExecuteNonQueryAsync()).ShouldBe(1);
        }

        await using var history = new NpgsqlCommand(
            """
            UPDATE revision r
            SET changed_at = CASE
                WHEN r.rev = 1 THEN ((now() AT TIME ZONE 'utc')::date - 16)::timestamp AT TIME ZONE 'utc'
                WHEN EXISTS (SELECT 1 FROM jsonb_array_elements(r.changes) c WHERE c ->> 'field' = @sprintField)
                    THEN ((now() AT TIME ZONE 'utc')::date - 15)::timestamp AT TIME ZONE 'utc'
                ELSE ((now() AT TIME ZONE 'utc')::date - 10)::timestamp AT TIME ZONE 'utc' END
            WHERE r.work_item_id = ANY(@ids)
            """,
            connection);
        history.Parameters.AddWithValue("sprintField", FieldIds.Sprint);
        history.Parameters.AddWithValue("ids", storyIds);
        (await history.ExecuteNonQueryAsync()).ShouldBeGreaterThan(0);
    }
}
