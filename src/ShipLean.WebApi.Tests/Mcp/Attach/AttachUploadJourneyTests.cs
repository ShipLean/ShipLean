using System.Net;
using Dapper;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Attach;

// An agent attaches a screenshot without sending its bytes through the model: it asks attach_file for a URL, PUTs the PNG to it
// with nothing but the URL (what `curl -T` does), and sees the file in the item's attachments.
[TestFixture]
public class AttachUploadJourneyTests : McpFixture
{
    // A 1x1 PNG.
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    protected override bool WithProbeTools => false;

    [Test]
    public async Task Agent_WhenAskingForUrlAndPuttingPng_ShouldSeeItInTheItemsAttachments()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Screenshot goes here").InsertAsync(connection);
        var key = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature });
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var asked = await CallToolAsync(token, "attach_file", $$"""{"feature":"{{key}}","name":"screenshot.png","content_type":"image/png"}""");
        var path = new Uri(asked.Structured().GetProperty("upload_url").GetString()!).AbsolutePath;
        using var put = await Client.PutAsync(path, new ByteArrayContent(Png));

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var item = await CallToolAsync(token, "get_work_item", $$"""{"key":"{{key}}"}""");
        var attachment = item.Structured().GetProperty("attachments").EnumerateArray().Single();
        attachment.GetProperty("name").GetString().ShouldBe("screenshot.png");
        attachment.GetProperty("is_markdown").GetBoolean().ShouldBeFalse();
        (await connection.QuerySingleAsync<(long Size, string Type, string? Via)>(
            "SELECT size_bytes AS Size, content_type AS Type, uploaded_via AS Via FROM attachment WHERE work_item_id = @feature", new { feature }))
            .ShouldBe((Png.Length, "image/png", "attach_file"));
    }
}
