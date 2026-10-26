using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.WorkItems.Api;

// The Feature's own fields through the real endpoints: goal, status line and its Verified stamp, and the slug set at creation.
[TestFixture]
public class FeatureFieldsApiTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static JsonObject Create(string type, string title, string? slug = null)
        => new()
        {
            ["projectId"] = SeedIds.Projects.Ship,
            ["type"] = type,
            ["title"] = title,
            ["parentId"] = null,
            ["sprintId"] = null,
            ["changes"] = null,
            ["slug"] = slug,
        };

    private static JsonObject Patch(int expectedRevision, JsonObject changes)
        => new() { ["expectedRevision"] = expectedRevision, ["changes"] = changes };

    private static JsonObject Value(JsonNode? value) => new() { ["value"] = value };

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(status, text);
        return JsonSerializer.Deserialize<T>(text, Wire)!;
    }

    private async Task<WriteResult> CreateItemAsync(string type, string title, string? slug = null)
    {
        using var response = await Api.PostJsonAsync("api/work-items", Create(type, title, slug));
        return await ReadAsync<WriteResult>(response);
    }

    private async Task<WorkItemDto> GetAsync(Guid id)
    {
        using var response = await Api.GetAsync($"api/work-items/{id}");
        return await ReadAsync<WorkItemDto>(response);
    }

    [Test]
    public async Task Patch_WhenFeatureGoalAndStatusSet_ShouldReturnThemWithVerifiedStamp()
    {
        var feature = await CreateItemAsync("feature", "Goal and status");
        var changes = new JsonObject { ["goal"] = Value("Pay in two taps."), ["statusLine"] = Value("Staging works.") };

        using var response = await Api.PatchJsonAsync($"api/work-items/{feature.Id}", Patch(feature.Revision, changes));

        var written = await ReadAsync<WriteResult>(response);
        written.Changes.Select(c => c.Field).ShouldBe(["goal", "statusLine", "statusVerifiedAt"]);
        var item = await GetAsync(feature.Id);
        item.Goal.ShouldBe("Pay in two taps.");
        item.StatusLine.ShouldBe("Staging works.");
        var verified = item.StatusVerified.ShouldNotBeNull();
        verified.By.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Joel);
        verified.Stale.ShouldBeFalse();
        verified.AtLabel.ShouldBe("just now");
        verified.Agent.ShouldBeNull();
        verified.Via.ShouldBeNull();
    }

    [Test]
    public async Task Patch_WhenConfirmStatusOnly_ShouldWriteOneRevisionWithVerifiedAtOnly()
    {
        var feature = await CreateItemAsync("feature", "Confirmed");
        using var first = await Api.PatchJsonAsync($"api/work-items/{feature.Id}", Patch(feature.Revision, new JsonObject { ["statusLine"] = Value("Going well.") }));
        var set = await ReadAsync<WriteResult>(first);
        var before = (await GetAsync(feature.Id)).StatusVerified.ShouldNotBeNull();
        var body = Patch(set.Revision, []);
        body["changes"]!["confirmStatus"] = true;

        using var response = await Api.PatchJsonAsync($"api/work-items/{feature.Id}", body);

        var confirmed = await ReadAsync<WriteResult>(response);
        confirmed.Revision.ShouldBe(set.Revision + 1);
        confirmed.Changes.Select(c => c.Field).ShouldBe(["statusVerifiedAt"]);
        var item = await GetAsync(feature.Id);
        item.StatusLine.ShouldBe("Going well.");
        item.StatusVerified.ShouldNotBeNull().At.ShouldBeGreaterThan(before.At);
        using var historyResponse = await Api.GetAsync($"api/work-items/{feature.Id}/history");
        var history = await ReadAsync<HistoryDto>(historyResponse);
        history.Revisions[0].Rev.ShouldBe(confirmed.Revision);
        history.Revisions[0].Changes.Select(c => c.Field).ShouldBe(["statusVerifiedAt"]);
    }

    [Test]
    public async Task Patch_WhenStoryGetsGoal_ShouldReturnValidation()
    {
        var story = await CreateItemAsync("story", "Not a feature");

        using var response = await Api.PatchJsonAsync($"api/work-items/{story.Id}", Patch(story.Revision, new JsonObject { ["goal"] = Value("Nope") }));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await GetAsync(story.Id)).Revision.ShouldBe(story.Revision);
    }

    [Test]
    public async Task Patch_WhenStoryConfirmsStatus_ShouldReturnValidation()
    {
        var story = await CreateItemAsync("story", "Not a feature either");
        var body = Patch(story.Revision, []);
        body["changes"]!["confirmStatus"] = true;

        using var response = await Api.PatchJsonAsync($"api/work-items/{story.Id}", body);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [TestCase("goal", 1001)]
    [TestCase("statusLine", 501)]
    public async Task Patch_WhenTextIsTooLong_ShouldReturnValidation(string field, int length)
    {
        var feature = await CreateItemAsync("feature", $"Long {field}");

        using var response = await Api.PatchJsonAsync($"api/work-items/{feature.Id}", Patch(feature.Revision, new JsonObject { [field] = Value(new string('x', length)) }));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Patch_WhenStatusLineHasTwoLines_ShouldReturnValidation()
    {
        var feature = await CreateItemAsync("feature", "Two lines");

        using var response = await Api.PatchJsonAsync($"api/work-items/{feature.Id}", Patch(feature.Revision, new JsonObject { ["statusLine"] = Value("One\nTwo") }));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Create_WhenFeatureHasSlug_ShouldReturnItWithTodayAsStartedOn()
    {
        var created = await CreateItemAsync("feature", "Slugged", $"slug-{Guid.NewGuid():N}"[..20]);

        var item = await GetAsync(created.Id);

        item.Slug.ShouldNotBeNull().ShouldStartWith("slug-");
        item.StartedOn.ShouldBe(DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime));
        item.StartedLabel.ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task Create_WhenSlugTaken_ShouldReturnConflictSlugTaken()
    {
        var slug = $"taken-{Guid.NewGuid():N}"[..20];
        await CreateItemAsync("feature", "First", slug);

        using var clash = await Api.PostJsonAsync("api/work-items", Create("feature", "Second", slug));
        var error = await ApiClient.ReadApiErrorAsync(clash, HttpStatusCode.Conflict);
        error.Code.ShouldBe(ApiErrorCodes.SlugTaken);
        error.Field.ShouldBe("slug");
        error.Error.ShouldContain("slug");
    }

    [TestCase("Not Valid")]
    [TestCase("double--hyphen")]
    [TestCase("-leading")]
    [TestCase("")]
    public async Task Create_WhenSlugInvalid_ShouldReturnValidation(string slug)
    {
        using var response = await Api.PostJsonAsync("api/work-items", Create("feature", "Bad slug", slug));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Create_WhenStoryHasSlug_ShouldReturnValidation()
    {
        using var response = await Api.PostJsonAsync("api/work-items", Create("story", "Slugged story", "a-story"));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }
}
