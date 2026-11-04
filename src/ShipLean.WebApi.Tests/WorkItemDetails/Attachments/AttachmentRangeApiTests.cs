using System.Net;
using System.Net.Http.Headers;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Storage;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// Ranged downloads of a video through the real app, once over a folder (whose files start with a header the range must not
// count) and once over Postgres rows. The video is the repo's real MP4, so a wrong offset shows as the wrong bytes.
[TestFixture(BlobStoreKind.File)]
[TestFixture(BlobStoreKind.Postgres)]
public class AttachmentRangeApiTests(BlobStoreKind kind) : ApiFixture
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments";

    private static readonly string BlobRoot = Path.Combine(Path.GetTempPath(), $"shiplean-range-blobs-{Guid.NewGuid():N}");

    private static readonly byte[] Video = File.ReadAllBytes(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DemoFixtures", "demo-fixture.mp4"));

    protected override IReadOnlyDictionary<string, string?> HostSettings => kind == BlobStoreKind.Postgres
        ? new Dictionary<string, string?> { [BlobStoreServices.DbConnectionKey] = ConnectionString }
        : new Dictionary<string, string?> { [BlobStoreServices.RootKey] = BlobRoot };

    [OneTimeTearDown]
    public void DeleteBlobs()
    {
        if (Directory.Exists(BlobRoot))
        {
            Directory.Delete(BlobRoot, recursive: true);
        }
    }

    [Test]
    public async Task Content_WhenRangeFromStart_ShouldBe206WithThoseBytesAndSafeHeaders()
    {
        var added = await UploadVideoAsync();

        using var response = await GetAsync(added.DownloadUrl, "bytes=0-99");

        response.StatusCode.ShouldBe(HttpStatusCode.PartialContent, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(Video[..100]);
        response.Content.Headers.ContentRange!.ToString().ShouldBe($"bytes 0-99/{Video.Length}");
        response.Headers.AcceptRanges.ShouldBe(["bytes"]);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("video/mp4");
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("Content-Security-Policy").ShouldBe(["sandbox; frame-ancestors 'none'"]);
        response.Headers.ETag!.Tag.ShouldBe($"\"{added.Id:N}\"");
    }

    [Test]
    public async Task Content_WhenOpenEndedRange_ShouldBe206FromThatOffset()
    {
        var added = await UploadVideoAsync();

        using var response = await GetAsync(added.DownloadUrl, "bytes=100-");

        response.StatusCode.ShouldBe(HttpStatusCode.PartialContent, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(Video[100..]);
        response.Content.Headers.ContentRange!.ToString().ShouldBe($"bytes 100-{Video.Length - 1}/{Video.Length}");
    }

    [Test]
    public async Task Content_WhenRangeStartsPastTheEnd_ShouldBe416()
    {
        var added = await UploadVideoAsync();

        using var response = await GetAsync(added.DownloadUrl, $"bytes={Video.Length + 10}-");

        response.StatusCode.ShouldBe(HttpStatusCode.RequestedRangeNotSatisfiable);
        response.Content.Headers.ContentRange!.ToString().ShouldBe($"bytes */{Video.Length}");
    }

    [Test]
    public async Task Content_WhenNoRange_ShouldBe200WholeAndAdvertiseRanges()
    {
        var added = await UploadVideoAsync();

        using var response = await GetAsync(added.DownloadUrl, null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(Video);
        response.Headers.AcceptRanges.ShouldBe(["bytes"]);
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
    }

    [Test]
    public async Task Content_WhenNotPlayable_ShouldIgnoreRangeAndAdvertiseNone()
    {
        var pdf = Enumerable.Range(0, 400).Select(i => (byte)(i % 251)).ToArray();
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "doc.pdf", "application/pdf", pdf);

        using var response = await GetAsync(added.DownloadUrl, "bytes=0-99");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(pdf);
        response.Headers.Contains("Accept-Ranges").ShouldBeFalse();
        response.Content.Headers.ContentRange.ShouldBeNull();
    }

    [Test]
    public async Task Content_WhenNotMemberAndRange_ShouldBeNotFoundButAMemberGetsTheBytes()
    {
        var added = await UploadVideoAsync();

        using var member = await GetAsync(added.DownloadUrl, "bytes=0-9", TestUser.Mei);
        using var outsider = await GetAsync(added.DownloadUrl, "bytes=0-9", TestUser.Kai);

        member.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
        (await ApiClient.ReadApiErrorAsync(outsider, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        outsider.Content.Headers.ContentRange.ShouldBeNull();
    }

    [Test]
    public async Task Download_WhenTokenAndRange_ShouldBe206WithThoseBytes()
    {
        var added = await UploadVideoAsync();
        await using var scope = Host.Services.CreateAsyncScope();
        var issued = await scope.ServiceProvider.GetRequiredService<ITransferTokens>().IssueAsync(
            new NewTransfer(TransferKind.Download, SeedIds.WorkItems.Ship90, added.Id, added.Name, "video/mp4", SeedIds.Users.Joel, null, "Agent"));

        using var response = await GetAsync($"api/downloads/{issued.Token}", "bytes=200-299");

        response.StatusCode.ShouldBe(HttpStatusCode.PartialContent, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(Video[200..300]);
        response.Content.Headers.ContentRange!.ToString().ShouldBe($"bytes 200-299/{Video.Length}");
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
    }

    [Test]
    public async Task Content_WhenBlobMissing_ShouldBeNotFoundBeforeAnyHeader()
    {
        var added = await UploadVideoAsync();
        await RemoveBlobAsync(added.Id);

        using var response = await GetAsync(added.DownloadUrl, "bytes=0-99");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        response.Content.Headers.ContentRange.ShouldBeNull();
        response.Headers.Contains("Accept-Ranges").ShouldBeFalse();
        response.Content.Headers.ContentDisposition.ShouldBeNull();
        response.Headers.ETag.ShouldBeNull();
    }

    private Task<AttachmentDto> UploadVideoAsync()
        => AttachmentUploads.UploadAsync(Client, ItemUrl, $"clip-{Guid.NewGuid():N}.mp4", "video/mp4", Video);

    private async Task RemoveBlobAsync(Guid attachmentId)
    {
        await using var connection = await OpenDatabaseAsync();
        var storageRef = await connection.ExecuteScalarAsync<string>("SELECT storage_ref FROM attachment WHERE id = @attachmentId", new { attachmentId });
        if (kind == BlobStoreKind.Postgres)
        {
            // Moves the row to another key, so the attachment's reference points at nothing.
            await connection.ExecuteAsync("UPDATE blob SET key = key || '.moved' WHERE key = @storageRef", new { storageRef });
        }
        else
        {
            File.Move(Path.Combine(BlobRoot, storageRef!), Path.Combine(BlobRoot, storageRef + ".moved"));
        }
    }

    private async Task<HttpResponseMessage> GetAsync(string url, string? range, TestUser? actingAs = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(ClientHeader.Name, ApiClient.ClientId);
        if (actingAs is not null)
        {
            request.Headers.Add(TestAuthentication.UserHeader, actingAs.Id.ToString());
        }

        if (range is not null)
        {
            request.Headers.Range = RangeHeaderValue.Parse(range);
        }

        return await Client.SendAsync(request);
    }
}
