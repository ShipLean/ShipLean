using System.Net;
using System.Text.Json;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backlog.Stories;

// POST backlog/items writes the rows, their tasks and their place in one go, or nothing.
[TestFixture]
public class BacklogAddApiTests : ApiFixture
{
    private static readonly string Url = $"api/projects/{SeedIds.Projects.Ship}/backlog";

    [Test]
    public async Task Add_WhenThreeItems_ShouldLandInInputOrderAtTop()
    {
        var created = await AddAsync(new { items = new[] { Story("First new"), Story("Second new"), Story("Third new") }, sprintId = (Guid?)null, placement = (object?)null });

        var keys = created.Select(c => c.Key).ToList();
        (await StoryKeysAsync()).Take(3).ShouldBe(keys);
        keys.Select(k => int.Parse(k.Split('-')[1])).ShouldBe(keys.Select(k => int.Parse(k.Split('-')[1])).Order());
    }

    [Test]
    public async Task Add_WhenPlacedBeforeRow_ShouldLandAsABlockAboveIt()
    {
        var before = new { slot = "before", beforeItemId = (Guid?)SeedIds.WorkItems.Ship153 };

        var created = await AddAsync(new { items = new[] { Story("Block one"), Story("Block two") }, sprintId = (Guid?)null, placement = before });

        var order = await StoryKeysAsync();
        var at = order.IndexOf("SHIP-153");
        order.Skip(at - 2).Take(3).ShouldBe([created[0].Key, created[1].Key, "SHIP-153"]);
    }

    [Test]
    public async Task Add_WhenPlacedLast_ShouldLandAfterTheEnd()
    {
        var created = await AddAsync(new { items = new[] { Story("Tail one"), Story("Tail two") }, sprintId = (Guid?)null, placement = new { slot = "last", beforeItemId = (Guid?)null } });

        (await StoryKeysAsync()).TakeLast(2).ShouldBe([created[0].Key, created[1].Key]);
    }

    [Test]
    public async Task Add_WhenSprintGiven_ShouldPutItemsAndTasksInIt()
    {
        var item = Story("In a sprint", tasks: [new { title = "A sprint task", points = (int?)null }]);

        var created = await AddAsync(new { items = new[] { item }, sprintId = (Guid?)SeedIds.Sprints.Sprint24, placement = (object?)null });

        created.Count.ShouldBe(2);
        foreach (var write in created)
        {
            (await RowAsync(write.Id)).SprintId.ShouldBe(SeedIds.Sprints.Sprint24);
        }
    }

    [Test]
    public async Task Add_WhenTasksGiven_ShouldParentThemToTheStory()
    {
        var item = Story("With tasks", tasks: [new { title = "Task one", points = (int?)1 }, new { title = "Task two", points = (int?)null }]);

        var created = await AddAsync(new { items = new[] { item }, sprintId = (Guid?)null, placement = (object?)null });

        created.Count.ShouldBe(3);
        (await RowAsync(created[0].Id)).Type.ShouldBe("story");
        var tasks = new[] { await RowAsync(created[1].Id), await RowAsync(created[2].Id) };
        tasks.ShouldAllBe(t => t.Type == "task" && t.ParentId == created[0].Id);
        tasks.Select(t => t.Title).ShouldBe(["Task one", "Task two"]);
        tasks[0].StoryPoints.ShouldBe(1m);
        int.Parse(created[1].Key.Split('-')[1]).ShouldBe(int.Parse(created[0].Key.Split('-')[1]) + 1);
    }

    [Test]
    public async Task Add_WhenPointsAndParentGiven_ShouldSetThem()
    {
        var item = Story("Pointed", points: 8, parentId: SeedIds.WorkItems.Ship101);

        var created = await AddAsync(new { items = new[] { item }, sprintId = (Guid?)null, placement = (object?)null });

        var row = await RowAsync(created.Single().Id);
        row.StoryPoints.ShouldBe(8m);
        row.ParentId.ShouldBe(SeedIds.WorkItems.Ship101);
    }

    [Test]
    public async Task Add_WhenOneTitleBlank_ShouldCreateNothing()
    {
        var before = await CountAsync();

        using var response = await Api.PostJsonAsync($"{Url}/items", new { items = new[] { Story("Fine"), Story("  ") }, sprintId = (Guid?)null, placement = (object?)null });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await CountAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Add_WhenOverLimitCountingTasks_ShouldAnswerValidation()
    {
        var before = await CountAsync();
        var tasks = Enumerable.Range(0, 100).Select(n => new { title = $"Task {n}", points = (int?)null }).ToList();

        using var response = await Api.PostJsonAsync($"{Url}/items", new { items = new[] { Story("Big", tasks: tasks) }, sprintId = (Guid?)null, placement = (object?)null });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await CountAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Add_WhenTaskAtTopLevel_ShouldAnswerValidation()
    {
        var before = await CountAsync();
        var item = new { type = "task", title = "Loose task", description = (string?)null, points = (int?)null, parentId = (Guid?)null, tasks = Array.Empty<object>() };

        using var response = await Api.PostJsonAsync($"{Url}/items", new { items = new[] { item }, sprintId = (Guid?)null, placement = (object?)null });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await CountAsync()).ShouldBe(before);
    }

    private static object Story(string title, int? points = null, Guid? parentId = null, IReadOnlyList<object>? tasks = null)
        => new { type = "story", title, description = (string?)null, points, parentId, tasks = tasks ?? [] };

    private async Task<IReadOnlyList<Written>> AddAsync(object body)
    {
        using var response = await Api.PostJsonAsync($"{Url}/items", body);
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return [.. JsonDocument.Parse(text).RootElement.GetProperty("created").EnumerateArray().Select(e => new Written(e.GetProperty("id").GetGuid(), e.GetProperty("key").GetString()!))];
    }

    private async Task<List<string>> StoryKeysAsync()
    {
        var view = await Api.GetBacklogAsync($"{Url}?mode=list");
        return [.. view.Rows.Select(row => row.Key)];
    }

    private async Task<int> CountAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM work_item");
    }

    private async Task<Row> RowAsync(Guid id)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<Row>("SELECT type, title, parent_id AS ParentId, sprint_id AS SprintId, story_points AS StoryPoints FROM work_item WHERE id = @id", new { id });
    }

    private sealed record Written(Guid Id, string Key);

    private sealed record Row(string Type, string Title, Guid? ParentId, Guid? SprintId, decimal? StoryPoints);
}
