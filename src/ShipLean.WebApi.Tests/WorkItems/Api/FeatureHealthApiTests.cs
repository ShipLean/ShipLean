using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Screens;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Screens;

namespace ShipLean.WebApi.Tests.WorkItems.Api;

// A Feature's health through the real endpoints: PATCH changes.health sets or clears it, stamps who set it, writes history
// in words, and GET reads it back. Health belongs to Features only.
[TestFixture]
public class FeatureHealthApiTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static JsonObject Create(string type, string title)
        => new()
        {
            ["projectId"] = SeedIds.Projects.Ship,
            ["type"] = type,
            ["title"] = title,
            ["parentId"] = null,
            ["sprintId"] = null,
            ["changes"] = null,
        };

    private static JsonObject Patch(int expectedRevision, JsonObject changes)
        => new() { ["expectedRevision"] = expectedRevision, ["changes"] = changes };

    private static JsonObject Health(string? value) => new() { ["health"] = new JsonObject { ["value"] = value } };

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(status, text);
        return JsonSerializer.Deserialize<T>(text, Wire)!;
    }

    private async Task<WriteResult> CreateItemAsync(string type, string title)
    {
        using var response = await Api.PostJsonAsync("api/work-items", Create(type, title));
        return await ReadAsync<WriteResult>(response);
    }

    private async Task<WriteResult> PatchAsync(Guid id, int expectedRevision, JsonObject changes)
    {
        using var response = await Api.PatchJsonAsync($"api/work-items/{id}", Patch(expectedRevision, changes));
        return await ReadAsync<WriteResult>(response);
    }

    private async Task<WorkItemDto> GetAsync(Guid id)
    {
        using var response = await Api.GetAsync($"api/work-items/{id}");
        return await ReadAsync<WorkItemDto>(response);
    }

    [Test]
    public async Task Patch_WhenFeatureHealthSet_ShouldReturnItWithSetStamp()
    {
        var feature = await CreateItemAsync("feature", "Health set");

        var written = await PatchAsync(feature.Id, feature.Revision, Health("atRisk"));

        written.Revision.ShouldBe(feature.Revision + 1);
        written.Changes.Select(c => c.Field).ShouldBe(["health"]);
        var item = await GetAsync(feature.Id);
        item.Health.ShouldBe(FeatureHealth.AtRisk);
        var set = item.HealthSet.ShouldNotBeNull();
        set.By.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Joel);
        set.Agent.ShouldBeNull();
        set.AtLabel.ShouldBe("just now");
    }

    [Test]
    public async Task Patch_WhenSameHealthWrittenAgain_ShouldStampItAgainWithoutARevision()
    {
        var feature = await CreateItemAsync("feature", "Health confirmed");
        var first = await PatchAsync(feature.Id, feature.Revision, Health("onTrack"));
        var before = (await GetAsync(feature.Id)).HealthSet.ShouldNotBeNull();

        var again = await PatchAsync(feature.Id, first.Revision, Health("onTrack"));

        again.Revision.ShouldBe(first.Revision);
        again.Changes.ShouldBeEmpty();
        var item = await GetAsync(feature.Id);
        item.Health.ShouldBe(FeatureHealth.OnTrack);
        item.HealthSet.ShouldNotBeNull().At.ShouldBeGreaterThan(before.At);
    }

    [Test]
    public async Task Patch_WhenHealthCleared_ShouldClearHealthAndStamp()
    {
        var feature = await CreateItemAsync("feature", "Health cleared");
        var set = await PatchAsync(feature.Id, feature.Revision, Health("blocked"));

        var cleared = await PatchAsync(feature.Id, set.Revision, Health(null));

        cleared.Changes.Select(c => c.Field).ShouldBe(["health"]);
        var item = await GetAsync(feature.Id);
        item.Health.ShouldBeNull();
        item.HealthSet.ShouldBeNull();
        await using var connection = await OpenDatabaseAsync();
        (await ScreensRows.HealthAsync(connection, feature.Id)).ShouldBe(new ScreensRows.Health(null, null, null, null));
    }

    [Test]
    public async Task Patch_WhenStoryGetsHealth_ShouldReturnValidationOnHealth()
    {
        var story = await CreateItemAsync("story", "Not a feature");

        using var response = await Api.PatchJsonAsync($"api/work-items/{story.Id}", Patch(story.Revision, Health("atRisk")));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("health");
        var item = await GetAsync(story.Id);
        item.Revision.ShouldBe(story.Revision);
        item.Health.ShouldBeNull();
    }

    [Test]
    public async Task Patch_WhenHealthChanged_ShouldWriteHistoryWithLabels()
    {
        var feature = await CreateItemAsync("feature", "Health history");
        var atRisk = await PatchAsync(feature.Id, feature.Revision, Health("atRisk"));
        var blocked = await PatchAsync(feature.Id, atRisk.Revision, Health("blocked"));

        using var response = await Api.GetAsync($"api/work-items/{feature.Id}/history");

        var history = await ReadAsync<HistoryDto>(response);
        var newest = history.Revisions[0];
        newest.Rev.ShouldBe(blocked.Revision);
        var change = newest.Changes.ShouldHaveSingleItem();
        change.Field.ShouldBe("health");
        change.From.GetString().ShouldBe("atRisk");
        change.To.GetString().ShouldBe("blocked");
        change.FromLabel.ShouldBe("At risk");
        change.ToLabel.ShouldBe("Blocked");
        var first = history.Revisions.Single(r => r.Rev == atRisk.Revision).Changes.ShouldHaveSingleItem();
        first.FromLabel.ShouldBeNull();
        first.ToLabel.ShouldBe("At risk");
    }

    [Test]
    public async Task Patch_WhenHealthOnly_ShouldNotStampStatusVerified()
    {
        var feature = await CreateItemAsync("feature", "Health and status");
        var status = await PatchAsync(feature.Id, feature.Revision, new JsonObject { ["statusLine"] = new JsonObject { ["value"] = "Going well." } });
        var verified = (await GetAsync(feature.Id)).StatusVerified.ShouldNotBeNull();

        var health = await PatchAsync(feature.Id, status.Revision, Health("atRisk"));

        health.Changes.Select(c => c.Field).ShouldBe(["health"]);
        var item = await GetAsync(feature.Id);
        item.StatusVerified.ShouldNotBeNull().At.ShouldBe(verified.At);
        item.Health.ShouldBe(FeatureHealth.AtRisk);
    }

    [Test]
    public async Task Patch_WhenStatusOnly_ShouldLeaveHealthAndItsStamp()
    {
        var feature = await CreateItemAsync("feature", "Status after health");
        var health = await PatchAsync(feature.Id, feature.Revision, Health("blocked"));
        var set = (await GetAsync(feature.Id)).HealthSet.ShouldNotBeNull();

        var status = await PatchAsync(feature.Id, health.Revision, new JsonObject { ["statusLine"] = new JsonObject { ["value"] = "Waiting on legal." } });

        status.Changes.Select(c => c.Field).ShouldBe(["statusLine", "statusVerifiedAt"]);
        var item = await GetAsync(feature.Id);
        item.Health.ShouldBe(FeatureHealth.Blocked);
        item.HealthSet.ShouldNotBeNull().At.ShouldBe(set.At);
    }

    [Test]
    public async Task Patch_WhenStaleRevision_ShouldReturnConflict()
    {
        var feature = await CreateItemAsync("feature", "Raced health");
        await PatchAsync(feature.Id, feature.Revision, new JsonObject { ["title"] = new JsonObject { ["value"] = "Renamed first" } });

        using var response = await Api.PatchJsonAsync($"api/work-items/{feature.Id}", Patch(feature.Revision, Health("blocked")));

        var conflict = await ReadAsync<ConflictDto>(response, HttpStatusCode.Conflict);
        conflict.Code.ShouldBe(ApiErrorCodes.Conflict);
        conflict.Current.Health.ShouldBeNull();
        var item = await GetAsync(feature.Id);
        item.Health.ShouldBeNull();
        item.HealthSet.ShouldBeNull();
    }

    [Test]
    public async Task GetByKey_WhenFeatureHasHealth_ShouldReturnHealthAndHealthSet()
    {
        using var response = await Api.GetAsync($"api/projects/{SeedIds.Projects.Ship}/work-items/by-key/SHIP-102");

        var item = await ReadAsync<WorkItemDto>(response);
        item.Id.ShouldBe(SeedIds.Screens.AtRiskFeature);
        item.Health.ShouldBe(FeatureHealth.AtRisk);
        var set = item.HealthSet.ShouldNotBeNull();
        set.By.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Tom);
        set.AtLabel.ShouldNotBe("just now");
    }
}
