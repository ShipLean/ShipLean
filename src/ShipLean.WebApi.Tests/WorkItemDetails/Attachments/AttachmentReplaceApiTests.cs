using System.Net;
using Dapper;
using ShipLean.Contracts.Attachments;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// Uploading a name that is already on the item replaces the file: the old row is archived and the new one counts it.
[TestFixture]
public class AttachmentReplaceApiTests : ApiFixture
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments";

    [Test]
    public async Task Upload_WhenSameName_ShouldArchiveOldAndCountReplacement()
    {
        var first = await AttachmentUploads.UploadAsync(Client, ItemUrl, "plan.txt", "text/plain", [1]);
        first.ReplacedCount.ShouldBe(0);

        var second = await AttachmentUploads.UploadAsync(Client, ItemUrl, "plan.txt", "text/plain", [2, 2]);
        var third = await AttachmentUploads.UploadAsync(Client, ItemUrl, "plan.txt", "text/plain", [3, 3, 3]);

        second.ReplacedCount.ShouldBe(1);
        third.ReplacedCount.ShouldBe(2);
        var live = (await AttachmentWire.GetAsync<List<AttachmentDto>>(Api, ItemUrl)).Where(a => a.Name == "plan.txt").ToList();
        live.Select(a => a.Id).ShouldBe([third.Id]);
        live.Single().ReplacedCount.ShouldBe(2);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM attachment WHERE name = 'plan.txt' AND archived_at IS NOT NULL")).ShouldBe(2);
        using var oldDownload = await Api.GetAsync(first.DownloadUrl);
        oldDownload.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Upload_WhenOtherName_ShouldNotReplace()
    {
        await AttachmentUploads.UploadAsync(Client, ItemUrl, "keep-a.txt", "text/plain", [1]);

        var other = await AttachmentUploads.UploadAsync(Client, ItemUrl, "keep-b.txt", "text/plain", [1]);

        other.ReplacedCount.ShouldBe(0);
        (await AttachmentWire.GetAsync<List<AttachmentDto>>(Api, ItemUrl)).Select(a => a.Name).ShouldContain("keep-a.txt");
    }
}
