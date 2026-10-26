using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.WorkItems.Api;

// One seeded scenario for the work item endpoints: every test creates the items it needs in SHIP and reads them back.
[TestFixture]
public class WorkItemsApiTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static JsonNode Create(Guid projectId, string type, string title, Guid? parentId = null, string[]? tags = null)
        => new JsonObject
        {
            ["projectId"] = projectId,
            ["type"] = type,
            ["title"] = title,
            ["parentId"] = parentId,
            ["sprintId"] = null,
            ["changes"] = tags is null ? null : new JsonObject { ["tags"] = new JsonObject { ["value"] = new JsonArray([.. tags.Select(t => (JsonNode?)t)]) } },
        };

    private static JsonNode PatchTitle(int expectedRevision, string title)
        => new JsonObject { ["expectedRevision"] = expectedRevision, ["changes"] = new JsonObject { ["title"] = new JsonObject { ["value"] = title } } };

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(status, text);
        return JsonSerializer.Deserialize<T>(text, Wire)!;
    }

    private async Task<WriteResult> CreateItemAsync(string type, string title, Guid? parentId = null, string[]? tags = null)
    {
        using var response = await Api.PostJsonAsync("api/work-items", Create(SeedIds.Projects.Ship, type, title, parentId, tags));
        return await ReadAsync<WriteResult>(response);
    }

    private async Task<IReadOnlyList<WorkItemDto>> ListAsync(string query = "")
    {
        using var response = await Api.GetAsync($"api/projects/{SeedIds.Projects.Ship}/work-items{query}");
        return await ReadAsync<List<WorkItemDto>>(response);
    }

    [Test]
    public async Task Create_WhenValid_ShouldTakeTheNextKey()
    {
        using var projectResponse = await Api.GetAsync($"api/projects/{SeedIds.Projects.Ship}");
        var project = await ReadAsync<ProjectDto>(projectResponse);

        var first = await CreateItemAsync("story", "First of two");
        var second = await CreateItemAsync("story", "Second of two");

        first.Key.ShouldBe($"SHIP-{project.NextItemNumber}");
        second.Key.ShouldBe($"SHIP-{project.NextItemNumber + 1}");
        first.Revision.ShouldBe(1);
        using var byKey = await Api.GetAsync($"api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{first.Key.ToLowerInvariant()}");
        (await ReadAsync<WorkItemDto>(byKey)).Id.ShouldBe(first.Id);
        using var byId = await Api.GetAsync($"api/work-items/{second.Id}");
        (await ReadAsync<WorkItemDto>(byId)).Title.ShouldBe("Second of two");
    }

    [Test]
    public async Task Create_WhenStoryParentIsEpic_ShouldBeValidation()
    {
        var epic = await CreateItemAsync("epic", "An epic");

        using var response = await Api.PostJsonAsync("api/work-items", Create(SeedIds.Projects.Ship, "story", "Under an epic", epic.Id));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Create_WhenOtherProjectsId_ShouldBeNotFound()
    {
        using var response = await Api.PostJsonAsync("api/work-items", Create(SeedIds.Projects.Part, "story", "Not mine"));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task ByKey_WhenOtherProjectsKey_ShouldBeNotFound()
    {
        using var created = await Api.PostJsonAsync("api/work-items", Create(SeedIds.Projects.Ops, "story", "In another project"));
        var written = await ReadAsync<WriteResult>(created);

        using var response = await Api.GetAsync($"api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{written.Key}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Patch_WhenValid_ShouldReturnWriteResultAndBumpRevision()
    {
        var created = await CreateItemAsync("story", "Before");

        using var response = await Api.PatchJsonAsync($"api/work-items/{created.Id}", PatchTitle(created.Revision, "After"));

        var result = await ReadAsync<WriteResult>(response);
        result.Id.ShouldBe(created.Id);
        result.Revision.ShouldBe(created.Revision + 1);
        result.Changes.ShouldNotBeEmpty();
        using var read = await Api.GetAsync($"api/work-items/{created.Id}");
        (await ReadAsync<WorkItemDto>(read)).Title.ShouldBe("After");
    }

    [Test]
    public async Task Patch_WhenStaleRevision_ShouldReturn409WithTheirChanges()
    {
        var created = await CreateItemAsync("story", "Original");
        using var theirs = await Api.PatchJsonAsync($"api/work-items/{created.Id}", PatchTitle(created.Revision, "Theirs"));
        var theirResult = await ReadAsync<WriteResult>(theirs);

        using var response = await Api.PatchJsonAsync($"api/work-items/{created.Id}", PatchTitle(created.Revision, "Mine"));

        var conflict = await ReadAsync<ConflictDto>(response, HttpStatusCode.Conflict);
        conflict.Code.ShouldBe(ApiErrorCodes.Conflict);
        conflict.Current.Revision.ShouldBe(theirResult.Revision);
        conflict.Current.Title.ShouldBe("Theirs");
        conflict.TheirChanges.Select(r => r.Rev).ShouldBe([theirResult.Revision]);
    }

    [Test]
    public async Task List_WhenTypeGiven_ShouldReturnThatTypeInBacklogOrder()
    {
        var bugOne = await CreateItemAsync("bug", "A bug");
        var story = await CreateItemAsync("story", "A story");
        var bugTwo = await CreateItemAsync("bug", "Another bug");

        var all = await ListAsync();
        var bugs = await ListAsync("?type=bug");

        bugs.ShouldAllBe(i => i.Type.Id == ShipLean.Contracts.Refs.WorkItemTypeId.Bug);
        bugs.Select(i => i.Id).ShouldBe(all.Where(i => i.Type.Id == ShipLean.Contracts.Refs.WorkItemTypeId.Bug).Select(i => i.Id));
        var ids = bugs.Select(i => i.Id).ToList();
        ids.ShouldContain(bugOne.Id);
        ids.ShouldContain(bugTwo.Id);
        ids.ShouldNotContain(story.Id);
        var orders = bugs.Select(i => i.BacklogOrder).ToList();
        orders.ShouldBe([.. orders.Order(StringComparer.Ordinal)]);
        ids.IndexOf(bugTwo.Id).ShouldBeLessThan(ids.IndexOf(bugOne.Id));
    }

    [Test]
    public async Task Patch_WhenListsAreNull_ShouldTreatThemAsEmpty()
    {
        var created = await CreateItemAsync("story", "Before");
        var body = new JsonObject
        {
            ["expectedRevision"] = created.Revision,
            ["changes"] = new JsonObject
            {
                ["title"] = new JsonObject { ["value"] = "After" },
                ["addLinks"] = null,
                ["removeLinkIds"] = null,
            },
        };

        using var response = await Api.PatchJsonAsync($"api/work-items/{created.Id}", body);

        (await ReadAsync<WriteResult>(response)).Revision.ShouldBe(created.Revision + 1);
    }

    [Test]
    public async Task Patch_WhenExpectedRevisionIsMissing_ShouldBeValidation()
    {
        var created = await CreateItemAsync("story", "Before");
        var body = new JsonObject { ["changes"] = new JsonObject { ["title"] = new JsonObject { ["value"] = "After" } } };

        using var response = await Api.PatchJsonAsync($"api/work-items/{created.Id}", body);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Remove_ShouldMoveToRemovedAndDropFromList()
    {
        var created = await CreateItemAsync("story", "To remove");

        using var response = await Api.PostJsonAsync($"api/work-items/{created.Id}/remove", new { expectedRevision = created.Revision });

        var result = await ReadAsync<WriteResult>(response);
        result.Revision.ShouldBe(created.Revision + 1);
        (await ListAsync()).Select(i => i.Id).ShouldNotContain(created.Id);
        using var read = await Api.GetAsync($"api/work-items/{created.Id}");
        (await ReadAsync<WorkItemDto>(read)).State.Category.ShouldBe(ShipLean.Contracts.Refs.StateCategory.Removed);
    }

    [Test]
    public async Task Remove_WhenStale_ShouldReturn409()
    {
        var created = await CreateItemAsync("story", "Stale remove");

        using var response = await Api.PostJsonAsync($"api/work-items/{created.Id}/remove", new { expectedRevision = created.Revision + 5 });

        (await ReadAsync<ConflictDto>(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task Reorder_WhenBeforeGiven_ShouldPlaceBeforeIt()
    {
        await CreateItemAsync("story", "Reorder one");
        await CreateItemAsync("story", "Reorder two");
        var current = await ListAsync();
        var last = current[^1];
        var first = current[0];

        using var response = await Api.PostJsonAsync($"api/work-items/{last.Id}/reorder", new { expectedRevision = last.Revision, beforeItemId = first.Id });

        (await ReadAsync<WriteResult>(response)).Id.ShouldBe(last.Id);
        var after = (await ListAsync()).Select(i => i.Id).ToList();
        after.IndexOf(last.Id).ShouldBe(after.IndexOf(first.Id) - 1);
    }

    [Test]
    public async Task Reorder_WhenNoBefore_ShouldPlaceLast()
    {
        var current = await ListAsync();
        var first = current[0];

        using var response = await Api.PostJsonAsync($"api/work-items/{first.Id}/reorder", new { expectedRevision = first.Revision, beforeItemId = (Guid?)null });

        (await ReadAsync<WriteResult>(response)).Id.ShouldBe(first.Id);
        (await ListAsync())[^1].Id.ShouldBe(first.Id);
    }

    [Test]
    public async Task Tags_ShouldBeDistinctAcrossLiveItems()
    {
        await CreateItemAsync("story", "Tagged one", tags: ["zz-Beta", "zz-alpha"]);
        await CreateItemAsync("story", "Tagged two", tags: ["zz-Beta", "zz-gamma"]);
        var removed = await CreateItemAsync("story", "Tagged removed", tags: ["zz-gone"]);
        using var removal = await Api.PostJsonAsync($"api/work-items/{removed.Id}/remove", new { expectedRevision = removed.Revision });
        await ReadAsync<WriteResult>(removal);

        using var response = await Api.GetAsync($"api/projects/{SeedIds.Projects.Ship}/tags");

        var tags = await ReadAsync<List<string>>(response);
        tags.Where(t => t.StartsWith("zz-", StringComparison.Ordinal)).ShouldBe(["zz-alpha", "zz-Beta", "zz-gamma"]);
        tags.ShouldBe(tags.Distinct().ToList());
        tags.ShouldBe(tags.OrderBy(t => t.ToLowerInvariant(), StringComparer.Ordinal).ThenBy(t => t, StringComparer.Ordinal).ToList());
    }
}
