using Dapper;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// Two uploads of one name sent together: the item is serialised, so one replaces the other and exactly one row stays live.
[TestFixture]
public class AttachmentRaceApiTests : ApiFixture
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship93}/attachments";

    [Test]
    public async Task Upload_WhenTwoSameNameUploadsRace_ShouldLeaveOneLiveRow()
    {
        var first = AttachmentUploads.PostAsync(Client, ItemUrl, "race.bin", "application/octet-stream", [1, 2, 3]);
        var second = AttachmentUploads.PostAsync(Client, ItemUrl, "race.bin", "application/octet-stream", [4, 5, 6, 7]);

        var responses = await Task.WhenAll(first, second);

        responses.Select(r => r.StatusCode).ShouldAllBe(code => code == System.Net.HttpStatusCode.OK);
        await using var connection = await OpenDatabaseAsync();
        var live = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM attachment WHERE work_item_id = @id AND name = 'race.bin' AND archived_at IS NULL", new { id = SeedIds.WorkItems.Ship93 });
        var archived = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM attachment WHERE work_item_id = @id AND name = 'race.bin' AND archived_at IS NOT NULL", new { id = SeedIds.WorkItems.Ship93 });
        live.ShouldBe(1);
        archived.ShouldBe(1);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }
}
