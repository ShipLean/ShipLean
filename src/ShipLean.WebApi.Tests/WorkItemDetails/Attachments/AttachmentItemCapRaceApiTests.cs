using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// The item cap pinned small (1000-byte files, 1500 for the item) and two different files that cannot both fit, sent together.
[TestFixture]
public class AttachmentItemCapRaceApiTests : ApiFixture
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship93}/attachments";

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton(new AttachmentLimits(1000, 1500));

    [Test]
    public async Task Upload_WhenTwoUploadsRaceOverTheItemCap_ShouldStoreOnlyOneThatFits()
    {
        var first = AttachmentUploads.PostAsync(Client, ItemUrl, "a.bin", "application/octet-stream", new byte[1000]);
        var second = AttachmentUploads.PostAsync(Client, ItemUrl, "b.bin", "application/octet-stream", new byte[1000]);

        using var a = await first;
        using var b = await second;

        new[] { a.StatusCode, b.StatusCode }.Order().ShouldBe([HttpStatusCode.OK, HttpStatusCode.BadRequest]);
        await using var connection = await OpenDatabaseAsync();
        var live = await connection.ExecuteScalarAsync<long>(
            "SELECT coalesce(sum(size_bytes), 0)::bigint FROM attachment WHERE work_item_id = @id AND archived_at IS NULL", new { id = SeedIds.WorkItems.Ship93 });
        live.ShouldBe(1000);
    }
}
