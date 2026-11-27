using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Blobs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// SHIP-2338: the sweep deletes the bytes of archived videos once the grace period has passed and keeps every row; any other archived
// type keeps its blob. Files go to a folder of the test's own; archived_at is moved back in the database to pass the grace period.
[TestFixture]
public class ArchivedVideoSweeperTests : ApiFixture
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments";

    private static readonly string BlobRoot = Path.Combine(Path.GetTempPath(), $"shiplean-video-blobs-{Guid.NewGuid():N}");

    private readonly LocalDiskBlobStore _blobs = new(BlobRoot);

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<IBlobStore>(_blobs);

    [OneTimeTearDown]
    public void DeleteBlobs() => Directory.Delete(BlobRoot, recursive: true);

    [Test]
    public async Task Sweep_WhenVideoArchivedPastGrace_ShouldDeleteItsBlobAndKeepTheRow()
    {
        var video = await ArchivedAsync("old-demo.mp4", "video/mp4", TimeSpan.FromHours(2));

        await SweepAsync();

        (await _blobs.ExistsAsync(video.StorageRef, CancellationToken.None)).ShouldBeFalse();
        (await RowAsync(video.Id)).ShouldNotBeNull().BlobDeletedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task Sweep_WhenOtherTypeArchived_ShouldKeepItsBlob()
    {
        var image = await ArchivedAsync("old-shot.png", "image/png", TimeSpan.FromHours(2));

        await SweepAsync();

        (await _blobs.ExistsAsync(image.StorageRef, CancellationToken.None)).ShouldBeTrue();
        (await RowAsync(image.Id)).ShouldNotBeNull().BlobDeletedAt.ShouldBeNull();
    }

    [Test]
    public async Task Sweep_WhenVideoArchivedWithinGrace_ShouldKeepItsBlob()
    {
        var video = await ArchivedAsync("fresh-demo.mp4", "video/mp4", TimeSpan.FromMinutes(5));

        await SweepAsync();

        (await _blobs.ExistsAsync(video.StorageRef, CancellationToken.None)).ShouldBeTrue();
    }

    [Test]
    public async Task Sweep_WhenVideoIsLive_ShouldKeepItsBlob()
    {
        var live = await AttachmentUploads.UploadAsync(Client, ItemUrl, "live-demo.mp4", "video/mp4", [1, 2, 3]);

        await SweepAsync();

        var row = (await RowAsync(live.Id)).ShouldNotBeNull();
        (await _blobs.ExistsAsync(row.StorageRef, CancellationToken.None)).ShouldBeTrue();
    }

    private async Task<(Guid Id, string StorageRef)> ArchivedAsync(string name, string contentType, TimeSpan ago)
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, name, contentType, [1, 2, 3]);
        (await Api.PostJsonAsync($"api/attachments/{added.Id}/archive")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE attachment SET archived_at = now() - @ago WHERE id = @id", new { ago, id = added.Id });
        var row = (await RowAsync(added.Id)).ShouldNotBeNull();
        (await _blobs.ExistsAsync(row.StorageRef, CancellationToken.None)).ShouldBeTrue();
        return (added.Id, row.StorageRef);
    }

    private Task SweepAsync() => Host.Services.GetRequiredService<ArchivedVideoSweeper>().SweepAsync(CancellationToken.None);

    private async Task<Row?> RowAsync(Guid id)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT storage_ref AS StorageRef, blob_deleted_at AS BlobDeletedAt FROM attachment WHERE id = @id", new { id });
    }

    private sealed record Row(string StorageRef, DateTimeOffset? BlobDeletedAt);
}
