using System.Net;
using System.Text;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Payloads;

// The K1 contract end to end over the real route and the real IToolPayloads (no doubles): create_payload_upload issues,
// PUT api/uploads/payloads/{token} stores once, and FindAsync returns the body only to the same user, authorization and tool.
// Red until SHIP-984 replaces the stub store (Upload and Find throw NotImplementedException until then).
[TestFixture]
public class PayloadUploadContractTests : McpFixture
{
    protected override bool WithProbeTools => false;

    private IToolPayloads Payloads => Host.Services.GetRequiredService<IToolPayloads>();

    [Test]
    public async Task PayloadUploadContract_WhenUploadedThenFound_ShouldReturnTheBody()
    {
        var issued = await IssueAsync("capture_work");
        var body = Encoding.UTF8.GetBytes("""{"items":[{"type":"task","title":"From a file"}]}""");

        using var response = await PutAsync(issued.Path, body);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var found = await Payloads.FindAsync(issued.PayloadId, "capture_work", SeedIds.Users.Joel, issued.AuthorizationId);
        found.ShouldNotBeNull();
        found.Tool.ShouldBe("capture_work");
        found.Field.ShouldBeNull();
        found.Body.ShouldBe(body);
    }

    [Test]
    public async Task PayloadUploadContract_WhenUploadedTwice_ShouldRefuseTheSecond()
    {
        var issued = await IssueAsync("capture_work");
        (await PutAsync(issued.Path, Encoding.UTF8.GetBytes("""{"project":"SHIP"}"""))).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await PutAsync(issued.Path, Encoding.UTF8.GetBytes("""{"project":"PART"}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ApiClient.ReadApiErrorAsync(response)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task PayloadUploadContract_WhenFoundForAnotherTool_ShouldReturnNull()
    {
        var issued = await IssueAsync("capture_work");
        (await PutAsync(issued.Path, Encoding.UTF8.GetBytes("""{"project":"SHIP"}"""))).StatusCode.ShouldBe(HttpStatusCode.OK);

        var found = await Payloads.FindAsync(issued.PayloadId, "capture_plan", SeedIds.Users.Joel, issued.AuthorizationId);

        found.ShouldBeNull();
    }

    // Each call registers its own client, so the authorization it finds is the one that asked for the payload.
    private async Task<Issued> IssueAsync(string tool)
    {
        var clientName = $"Payload Agent {Guid.NewGuid():N}";
        var token = await TokenAsync(TestUser.Joel, ReadWrite, clientName);
        var reply = await CallToolAsync(token, "create_payload_upload", $$"""{"tool":"{{tool}}"}""");
        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var result = reply.Structured();
        await using var connection = await OpenDatabaseAsync();
        var authorizationId = await connection.QuerySingleAsync<Guid>(
            "SELECT a.id FROM oauth_authorization a JOIN oauth_client c ON c.client_id = a.client_id WHERE c.client_name = @clientName AND a.revoked_at IS NULL",
            new { clientName });
        return new Issued(result.GetProperty("payload").GetString()!, new Uri(result.GetProperty("upload_url").GetString()!).AbsolutePath, authorizationId);
    }

    private Task<HttpResponseMessage> PutAsync(string path, byte[] bytes) => Client.PutAsync(path, new ByteArrayContent(bytes));

    private sealed record Issued(string PayloadId, string Path, Guid AuthorizationId);
}
