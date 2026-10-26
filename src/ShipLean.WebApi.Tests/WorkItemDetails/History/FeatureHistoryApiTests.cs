using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.WorkItemDetails.History;

// What the History rail gets for the Feature-only fields: goal and status line as text, and the Verified stamp as an instant
// with the server's date labels. Slug and started-on are set once at creation and are not history fields.
[TestFixture]
public class FeatureHistoryApiTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    [Test]
    public async Task History_WhenStatusLineSet_ShouldLabelVerifiedAtWithDisplayDate()
    {
        var feature = await CreateFeatureAsync();
        await PatchAsync(feature, new JsonObject { ["statusLine"] = Value("Staging works.") });

        var revision = (await ReadHistoryAsync(feature.Id)).Revisions[0];

        var stamp = revision.Changes.Single(c => c.Field == "statusVerifiedAt");
        var at = stamp.To.GetDateTimeOffset();
        stamp.ToLabel.ShouldBe(DisplayDate.Format(at));
        stamp.FromLabel.ShouldBeNull();
        revision.Changes.Select(c => c.Field).ShouldBe(["statusLine", "statusVerifiedAt"]);
        revision.Changes.Single(c => c.Field == "statusLine").ToLabel.ShouldBeNull();
    }

    [Test]
    public async Task History_WhenStatusConfirmedAgain_ShouldLabelBothInstants()
    {
        var feature = await CreateFeatureAsync();
        var first = await PatchAsync(feature, new JsonObject { ["statusLine"] = Value("Staging works.") });
        await PatchAsync(first, new JsonObject { ["confirmStatus"] = true });

        var stamp = (await ReadHistoryAsync(feature.Id)).Revisions[0].Changes.ShouldHaveSingleItem();

        stamp.Field.ShouldBe("statusVerifiedAt");
        stamp.FromLabel.ShouldBe(DisplayDate.Format(stamp.From.GetDateTimeOffset()));
        stamp.ToLabel.ShouldBe(DisplayDate.Format(stamp.To.GetDateTimeOffset()));
    }

    [Test]
    public async Task History_WhenFeatureCreatedWithSlug_ShouldNotRecordSlugOrStartedOn()
    {
        var feature = await CreateFeatureAsync();

        var fields = (await ReadHistoryAsync(feature.Id)).Revisions.SelectMany(r => r.Changes).Select(c => c.Field);

        fields.ShouldNotContain("slug");
        fields.ShouldNotContain("startedOn");
    }

    private static JsonObject Value(JsonNode? value) => new() { ["value"] = value };

    private async Task<WriteResult> CreateFeatureAsync()
    {
        var body = new JsonObject
        {
            ["projectId"] = SeedIds.Projects.Ship,
            ["type"] = "feature",
            ["title"] = "History of a feature",
            ["parentId"] = null,
            ["sprintId"] = null,
            ["changes"] = null,
            ["slug"] = $"history-{Guid.NewGuid():N}"[..20],
        };
        using var response = await Api.PostJsonAsync("api/work-items", body);
        return await ReadAsync<WriteResult>(response);
    }

    private async Task<WriteResult> PatchAsync(WriteResult item, JsonObject changes)
    {
        var body = new JsonObject { ["expectedRevision"] = item.Revision, ["changes"] = changes };
        using var response = await Api.PatchJsonAsync($"api/work-items/{item.Id}", body);
        return await ReadAsync<WriteResult>(response);
    }

    private async Task<HistoryDto> ReadHistoryAsync(Guid id)
    {
        using var response = await Api.GetAsync($"api/work-items/{id}/history");
        return await ReadAsync<HistoryDto>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<T>(text, Wire)!;
    }
}
