using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.TaskChanges;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.TaskChanges.Domain;

// IRevisionNotes writes a history line that changes no column.
[TestFixture]
public class RevisionNotesTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    [Test]
    public async Task RevisionNotes_WhenAdded_ShouldShowInHistoryWithLabel()
    {
        var body = new JsonObject { ["projectId"] = SeedIds.Projects.Ship, ["type"] = "task", ["title"] = "Noted", ["parentId"] = null, ["sprintId"] = null, ["changes"] = null };
        using var created = await Api.PostJsonAsync("api/work-items", body);
        var item = JsonSerializer.Deserialize<WriteResult>(await created.Content.ReadAsStringAsync(), Wire)!;

        await using (var scope = Host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRevisionNotes>()
                .AddAsync(item.Id, FieldIds.Commits, "Commits +3 (acme/storefront)", WriteOptions.User);
        }

        using var response = await Api.GetAsync($"api/work-items/{item.Id}/history");
        var history = JsonSerializer.Deserialize<HistoryDto>(await response.Content.ReadAsStringAsync(), Wire)!;
        var note = history.Revisions.First(r => r.Rev == item.Revision + 1);
        note.Changes.Count.ShouldBe(1);
        note.Changes[0].Field.ShouldBe("commits");
        note.Changes[0].ToLabel.ShouldBe("Commits +3 (acme/storefront)");
        note.Changes[0].FromLabel.ShouldBeNull();
    }
}
