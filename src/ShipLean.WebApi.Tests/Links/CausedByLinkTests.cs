using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using Shouldly;

namespace ShipLean.WebApi.Tests.Links;

// A Caused by link goes from a Bug to the Task that introduced it (SHIP-1398 item D18): the add endpoint, and what each end reads.
[TestFixture]
public class CausedByLinkTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private async Task<(Guid Id, string Key)> ArrangeAsync(string type, string title)
    {
        await using var connection = await OpenDatabaseAsync();
        var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType(type).WithTitle(title).InsertAsync(connection);
        return (id, await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }));
    }

    private Task<HttpResponseMessage> AddAsync(Guid from, string toKey) => Api.PostJsonAsync($"api/work-items/{from}/work-item-links", new { targetKey = toKey, type = "causedBy" });

    private async Task<LinkCardsDto> ListAsync(Guid id)
    {
        using var response = await Api.GetAsync($"api/work-items/{id}/link-cards");
        return (await response.Content.ReadFromJsonAsync<LinkCardsDto>(Wire))!;
    }

    private async Task<WorkItemDto> ReadAsync(string key)
    {
        using var response = await Api.GetAsync($"api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{key}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<WorkItemDto>(Wire))!;
    }

    [Test]
    public async Task Add_WhenBugToTask_ShouldShowCausedOnTask()
    {
        var bug = await ArrangeAsync("bug", "Escaped to prod");
        var task = await ArrangeAsync("task", "Introduced it");

        using var response = await AddAsync(bug.Id, task.Key);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var onBug = (await ListAsync(bug.Id)).WorkItems.ShouldHaveSingleItem();
        onBug.Type.ShouldBe(LinkType.CausedBy);
        onBug.Key.ShouldBe(task.Key);
        var caused = (await ReadAsync(task.Key)).Caused.ShouldHaveSingleItem();
        caused.Key.ShouldBe(bug.Key);
        caused.Title.ShouldBe("Escaped to prod");
        (await ReadAsync(bug.Key)).Caused.ShouldBeEmpty();
    }

    [Test]
    public async Task Add_WhenStoryToTask_ShouldReturnValidation()
    {
        var story = await ArrangeAsync("story", "Not a bug");
        var task = await ArrangeAsync("task", "A task");

        using var response = await AddAsync(story.Id, task.Key);

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("type");
        (await ListAsync(story.Id)).WorkItems.ShouldBeEmpty();
    }

    [Test]
    public async Task Add_WhenBugToStory_ShouldReturnValidation()
    {
        var bug = await ArrangeAsync("bug", "A bug");
        var story = await ArrangeAsync("story", "Not a task");

        using var response = await AddAsync(bug.Id, story.Key);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Add_WhenTaskToBug_ShouldReturnValidation()
    {
        var bug = await ArrangeAsync("bug", "A bug");
        var task = await ArrangeAsync("task", "A task");

        using var response = await AddAsync(task.Id, bug.Key);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await ListAsync(bug.Id)).WorkItems.ShouldBeEmpty();
    }

    [Test]
    public async Task Add_WhenRepeated_ShouldReturnLinkDuplicate()
    {
        var bug = await ArrangeAsync("bug", "Twice");
        var task = await ArrangeAsync("task", "Cause");
        (await AddAsync(bug.Id, task.Key)).StatusCode.ShouldBe(HttpStatusCode.Created);

        using var response = await AddAsync(bug.Id, task.Key);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.LinkDuplicate);
    }
}
