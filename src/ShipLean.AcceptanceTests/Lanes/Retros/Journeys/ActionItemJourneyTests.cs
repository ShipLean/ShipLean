using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Retros;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Journeys;

// Joel turns an agent's Can be improved point into an Action item. It lands in the Stories backlog with no sprint, is pulled
// into the next sprint from Insights, and then shows on that sprint's page (its points in the figures) and on the board.
[TestFixture]
public class ActionItemJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private const decimal ActionPoints = 3;
    private static readonly Guid ProjectId = SeedIds.Projects.Ship;
    private static readonly Guid RetroSprintId = SeedIds.Sprints.Sprint19;

    [Test]
    public async Task CreateActionItem_WhenFromImprovePoint_ShouldBePullableIntoSprint()
    {
        var pointText = "Estimates ignore review latency";
        await ArrangeImprovePointAsync(pointText);
        var retro = new SprintRetroPage(Page);
        await retro.OpenAsync(Project, RetroSprintId);

        // The note's "+ Action item" opens the dialog, which starts New with the point selected and its text as the title.
        await retro.AddAction(pointText).ClickAsync();
        await Expect(retro.Dialog.Dialog).ToBeVisibleAsync();
        await Expect(retro.Dialog.Title).ToHaveValueAsync(pointText);
        var created = await Page.RunAndWaitForApiAsync($"/api/projects/{ProjectId}/retros/actions", "POST", () => retro.Dialog.Save.ClickAsync());
        created.Ok.ShouldBeTrue(await created.TextAsync());
        using var reply = JsonDocument.Parse(await created.TextAsync());
        var key = reply.RootElement.GetProperty("action").GetProperty("key").GetString()!;
        key.ShouldNotBeNullOrEmpty();
        await Expect(retro.AddAction(pointText)).ToHaveCountAsync(0);
        await Expect(retro.RailActions).ToContainTextAsync(key);

        // Points are arranged on the item: the section creates it without any.
        await using (var connection = await OpenAsync())
        {
            await connection.ExecuteAsync("UPDATE work_item SET story_points = @points WHERE project_id = @ProjectId AND key = @key", new { points = ActionPoints, ProjectId, key });
        }

        var backlog = new StoriesBacklog(Page);
        await backlog.OpenAsync(Project);
        await Expect(backlog.Rows.Filter(new() { HasText = key })).ToBeVisibleAsync();

        var (nextId, nextName) = await NextSprintAsync();
        var sprintPage = new SprintBacklogPage(Page);
        await sprintPage.OpenAsync(Project, nextId);
        var committedBefore = decimal.Parse(Regex.Match(await sprintPage.Committed.InnerTextAsync(), @"^\d+(\.\d+)?").Value, CultureInfo.InvariantCulture);
        await Expect(sprintPage.StoryRow(key)).ToHaveCountAsync(0);

        var insights = new RetroInsightsPage(Page);
        await insights.OpenAsync(Project, "actions");
        await Expect(insights.ActionRowOf(pointText)).ToBeVisibleAsync();
        var pulled = await Page.RunAndWaitForResponseAsync(
            () => insights.Pull(pointText).ClickAsync(),
            r => r.Request.Method == "PATCH" && new Uri(r.Url).AbsolutePath.StartsWith("/api/work-items/", StringComparison.Ordinal));
        pulled.Ok.ShouldBeTrue(await pulled.TextAsync());
        await Expect(insights.Pull(pointText)).ToHaveCountAsync(0);
        await Expect(insights.ActionRowOf(pointText)).ToContainTextAsync(nextName);

        await sprintPage.OpenAsync(Project, nextId);
        await Expect(sprintPage.StoryRow(key)).ToBeVisibleAsync();
        var committedAfter = committedBefore + ActionPoints;
        await Expect(sprintPage.Committed).ToHaveTextAsync(Leading(committedAfter));

        var board = new BoardScreen(Page);
        await board.OpenAsync(Project, "stories");
        await Expect(board.Card(key)).ToBeVisibleAsync();
    }

    private static Regex Leading(decimal points) => new($"^{Regex.Escape(points.ToString("0.##", CultureInfo.InvariantCulture))}(?![\\d.])");

    // The earliest sprint that has not started, as the Pull button picks it.
    private async Task<(Guid Id, string Name)> NextSprintAsync()
    {
        var response = await Page.Context.APIRequest.GetAsync($"/api/projects/{ProjectId}/sprints");
        response.Ok.ShouldBeTrue(await response.TextAsync());
        using var sprints = JsonDocument.Parse(await response.TextAsync());
        var next = sprints.RootElement.EnumerateArray()
            .Where(s => s.GetProperty("status").GetString() == "future")
            .OrderBy(s => s.GetProperty("startDate").GetString(), StringComparer.Ordinal)
            .First();
        return (next.GetProperty("id").GetGuid(), next.GetProperty("name").GetString()!);
    }

    private async Task<Guid> ArrangeImprovePointAsync(string text)
    {
        await using var connection = await OpenAsync();
        var seed = new RetroSeed(connection, ProjectId);
        var retroId = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, SprintId: RetroSprintId, AgentName: $"agent-{Guid.NewGuid():N}"));
        return await seed.PointAsync(retroId, new RetroSeedPoint(RetroCategory.Improve, text));
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}
