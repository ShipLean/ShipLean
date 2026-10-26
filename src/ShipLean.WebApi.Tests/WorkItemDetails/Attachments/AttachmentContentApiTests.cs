using System.Net;
using System.Text;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// The attachment-id endpoints (download, archive) through the real app, with the size limit pinned at 1 KiB. Joel is the project admin and the
// default caller; Mei is a plain member; Kai is in no project.
[TestFixture]
public class AttachmentContentApiTests : ApiFixture
{
    private const int Limit = 1024;

    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments";

    private static readonly string BlobRoot = Path.Combine(Path.GetTempPath(), $"shiplean-blobs-{Guid.NewGuid():N}");

    // The limit is pinned at 1 KiB and the files go to a folder of the test's own, not the app's data folder.
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton(new AttachmentLimits(Limit));
        services.AddSingleton<IBlobStore>(new LocalDiskBlobStore(BlobRoot));
    };

    [OneTimeTearDown]
    public void DeleteBlobs() => Directory.Delete(BlobRoot, recursive: true);

    [Test]
    public async Task Upload_WhenMember_ShouldDownloadSameBytesWithSafeHeaders()
    {
        var bytes = Encoding.UTF8.GetBytes("<script>alert(1)</script>");

        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "notes.html", "text/html", bytes, TestUser.Mei);
        using var download = await Api.GetAsync(added.DownloadUrl);

        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
        download.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        download.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        download.Content.Headers.ContentDisposition.FileName.ShouldBe("notes.html");
        download.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        download.Headers.GetValues("Content-Security-Policy").ShouldBe(["sandbox; frame-ancestors 'none'"]);
        added.UploadedBy.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Mei);
        (await Api.GetJsonAsync<List<AttachmentDto>>(ItemUrl)).ShouldContain(a => a.Id == added.Id);
    }

    [Test]
    public async Task Attachment_WhenNotMember_ShouldNotDownloadOrArchive()
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "members.txt", "text/plain", [1, 2, 3]);
        var kai = Api.ActingAs(TestUser.Kai);

        using var download = await kai.GetAsync(added.DownloadUrl);
        using var archive = await kai.PostJsonAsync($"api/attachments/{added.Id}/archive");

        foreach (var response in new[] { download, archive })
        {
            var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound);
            error.Code.ShouldBe(ApiErrorCodes.NotFound);
        }

        (await Api.GetJsonAsync<List<AttachmentDto>>(ItemUrl)).ShouldContain(a => a.Id == added.Id);
    }

    [Test]
    public async Task Archive_WhenMember_ShouldHideItAndKeepTheRowAndBlob()
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "gone.txt", "text/plain", [9, 9]);

        using var response = await Api.ActingAs(TestUser.Mei).PostJsonAsync($"api/attachments/{added.Id}/archive");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Api.GetJsonAsync<List<AttachmentDto>>(ItemUrl)).ShouldNotContain(a => a.Id == added.Id);
        using var download = await Api.GetAsync(added.DownloadUrl);
        (await ApiClient.ReadApiErrorAsync(download, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<bool>("SELECT archived_at IS NOT NULL FROM attachment WHERE id = @id", new { id = added.Id })).ShouldBeTrue();
    }

    [Test]
    public async Task Archive_WhenArchived_ShouldPublishAttachmentsChanged()
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "bye.txt", "text/plain", [1]);
        using var watch = Watch();

        using var response = await Api.PostJsonAsync($"api/attachments/{added.Id}/archive");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await watch.NextAsync<AttachmentsChanged>(e => e.WorkItemId == SeedIds.WorkItems.Ship90)).ProjectId.ShouldBe(SeedIds.Projects.Ship);
    }

    private ProjectEventWatch Watch() => new(Host.Services.GetRequiredService<IProjectEvents>(), SeedIds.Projects.Ship);
}
