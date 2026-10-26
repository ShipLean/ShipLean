using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.WorkItems.Api;

// Still true through the real endpoint: one revision that changes only Verified at, a conflict on a stale revision, access first.
[TestFixture]
public class StatusConfirmTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static JsonObject Create(Guid projectId, string type, string title)
        => new()
        {
            ["projectId"] = projectId,
            ["type"] = type,
            ["title"] = title,
            ["parentId"] = null,
            ["sprintId"] = null,
            ["changes"] = null,
        };

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(status, text);
        return JsonSerializer.Deserialize<T>(text, Wire)!;
    }

    private async Task<WriteResult> CreateFeatureAsync(Guid projectId, string title)
    {
        using var response = await Api.PostJsonAsync("api/work-items", Create(projectId, "feature", title));
        return await ReadAsync<WriteResult>(response);
    }

    private static JsonObject Confirm(int revision) => new() { ["revision"] = revision };

    [Test]
    public async Task ConfirmStatus_WhenCurrentRevision_ShouldStampVerifiedAndBumpRevision()
    {
        var feature = await CreateFeatureAsync(SeedIds.Projects.Ship, "Still true");
        using var setStatus = await Api.PatchJsonAsync(
            $"api/work-items/{feature.Id}",
            new JsonObject { ["expectedRevision"] = feature.Revision, ["changes"] = new JsonObject { ["statusLine"] = new JsonObject { ["value"] = "Going well." } } });
        var set = await ReadAsync<WriteResult>(setStatus);

        using var response = await Api.PostJsonAsync($"api/work-items/{feature.Id}/status/confirm", Confirm(set.Revision));

        var item = await ReadAsync<WorkItemDto>(response);
        item.Revision.ShouldBe(set.Revision + 1);
        item.StatusLine.ShouldBe("Going well.");
        item.StatusVerified.ShouldNotBeNull().AtLabel.ShouldBe("just now");
        using var historyResponse = await Api.GetAsync($"api/work-items/{feature.Id}/history");
        var history = await ReadAsync<HistoryDto>(historyResponse);
        history.Revisions[0].Rev.ShouldBe(item.Revision);
        history.Revisions[0].Changes.Select(c => c.Field).ShouldBe(["statusVerifiedAt"]);
    }

    [Test]
    public async Task ConfirmStatus_WhenStaleRevision_ShouldReturn409Conflict()
    {
        var feature = await CreateFeatureAsync(SeedIds.Projects.Ship, "Stale");

        using var response = await Api.PostJsonAsync($"api/work-items/{feature.Id}/status/confirm", Confirm(feature.Revision + 5));

        var conflict = await ReadAsync<ConflictDto>(response, HttpStatusCode.Conflict);
        conflict.Code.ShouldBe(ApiErrorCodes.Conflict);
        conflict.Current.Revision.ShouldBe(feature.Revision);
    }

    [Test]
    public async Task ConfirmStatus_WhenNonMember_ShouldReturnNotFound()
    {
        var feature = await CreateFeatureAsync(SeedIds.Projects.Ship, "Not yours");

        using var denied = await Api.ActingAs(TestUser.Kai).PostJsonAsync($"api/work-items/{feature.Id}/status/confirm", Confirm(feature.Revision));

        (await ApiClient.ReadApiErrorAsync(denied, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
