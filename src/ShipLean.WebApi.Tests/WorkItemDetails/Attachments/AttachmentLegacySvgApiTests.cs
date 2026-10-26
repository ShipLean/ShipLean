using System.Text;
using Dapper;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// An SVG is shown in place only when it was sanitised on upload; one stored before that (or never sanitised) downloads.
[TestFixture]
public class AttachmentLegacySvgApiTests : ApiFixture
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments";

    private static readonly byte[] Svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"1\" height=\"1\"/></svg>");

    [Test]
    public async Task Download_WhenSvgWasNotSanitized_ShouldServeAsAttachment()
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "legacy.svg", "image/svg+xml", Svg);
        await using (var connection = await OpenDatabaseAsync())
        {
            // The row as an upload from before sanitising would have left it.
            await connection.ExecuteAsync("UPDATE attachment SET sanitized = false WHERE id = @id", new { id = added.Id });
        }

        using var download = await Api.GetAsync(added.DownloadUrl);

        download.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        download.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        download.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("default-src 'none'");
    }

    [Test]
    public async Task Download_WhenSvgWasSanitized_ShouldServeInline()
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "fresh.svg", "image/svg+xml", Svg);

        using var download = await Api.GetAsync(added.DownloadUrl);

        download.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
        download.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("default-src 'none'");
    }
}
