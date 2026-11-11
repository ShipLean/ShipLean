using System.Net;
using System.Net.Http.Json;
using Dapper;
using ShipLean.Contracts.Attachments;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Payloads;

// The payload route (api/uploads/payloads/{token}) sits beside attach_file's api/uploads/{token}: an attachment upload URL
// must still reach the attachment upload.
[TestFixture]
public class CreatePayloadUploadRoutingTests : McpFixture
{
    protected override bool WithProbeTools => false;

    [Test]
    public async Task AttachUpload_WhenPayloadRouteAdded_ShouldStillRouteToAttachment()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Route beside payloads").InsertAsync(connection);
        var key = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature });
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var reply = await CallToolAsync(token, "attach_file", $$"""{"feature":"{{key}}","name":"routed.txt","content_type":"text/plain"}""");
        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var path = new Uri(reply.Structured().GetProperty("upload_url").GetString()!).AbsolutePath;

        using var response = await Client.PutAsync(path, new ByteArrayContent([1, 2, 3]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var uploaded = await response.Content.ReadFromJsonAsync<UploadedAttachmentDto>();
        uploaded!.Name.ShouldBe("routed.txt");
        (await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM attachment WHERE work_item_id = @feature AND name = 'routed.txt' AND archived_at IS NULL", new { feature })).ShouldBe(1);
    }
}
