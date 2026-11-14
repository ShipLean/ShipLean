using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Timeline;

// GET api/projects/{projectId}/work-items/by-key/{itemKey}/timeline through the real app (C2). Joel is a member of Ship and not
// of Part. The database is shared by the class, so every test arranges its own items.
[TestFixture]
public class FeatureTimelineApiTests : ApiFixture
{
    private static readonly Guid Ship = SeedIds.Projects.Ship;

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static string Url(Guid projectId, string key) => $"api/projects/{projectId}/work-items/by-key/{key}/timeline";

    private async Task<(Guid Id, string Key)> ItemAsync(Guid projectId, Action<WorkItemBuilder> configure)
    {
        var builder = new WorkItemBuilder(projectId);
        configure(builder);
        await using var connection = await OpenDatabaseAsync();
        var id = await builder.InsertAsync(connection);
        var key = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });
        return (id, key!);
    }

    [Test]
    public async Task Get_WhenFeature_ShouldReturnItsHeaderAndLink()
    {
        var epic = await ItemAsync(Ship, b => b.WithType("epic").WithTitle("Timeline epic"));
        var feature = await ItemAsync(Ship, b => b.WithType("feature").WithTitle("Timeline feature").WithStateNamed("Active").WithParent(epic.Id));

        using var response = await Api.GetAsync(Url(Ship, feature.Key));

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var view = JsonSerializer.Deserialize<FeatureTimelineViewDto>(text, Wire)!;
        view.Feature.ShouldBe(new TimelineFeatureDto(feature.Id, feature.Key, "Timeline feature", "Active", StateCategory.InProgress, new TimelineEpicDto(epic.Key, "Timeline epic")));
        view.Links.Ui.ShouldBe($"/p/SHIP/items/{feature.Key}/timeline");
    }

    [Test]
    public async Task Get_WhenStoryKey_ShouldReturnNotFound()
    {
        var feature = await ItemAsync(Ship, b => b.WithType("feature"));
        var story = await ItemAsync(Ship, b => b.WithType("story").WithParent(feature.Id));

        using var response = await Api.GetAsync(Url(Ship, story.Key));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Get_WhenUnknownKey_ShouldReturnNotFound()
    {
        using var response = await Api.GetAsync(Url(Ship, "SHIP-999999"));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Get_WhenOtherProjectsFeatureKey_ShouldReturnNotFound()
    {
        var other = await ItemAsync(SeedIds.Projects.Part, b => b.WithType("feature"));

        using var response = await Api.GetAsync(Url(Ship, other.Key));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
