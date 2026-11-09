using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Payloads;

// create_payload_upload asks for an upload URL for another tool's arguments: it stores nothing and needs Write.
[TestFixture]
public class CreatePayloadUploadTests : McpFixture
{
    protected override bool WithProbeTools => false;

    [Test]
    public async Task CreatePayloadUpload_WhenCalled_ShouldReturnUploadUrlAndPayloadId()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var now = Host.Services.GetRequiredService<TimeProvider>().GetUtcNow();

        var reply = await CallToolAsync(token, "create_payload_upload", """{"tool":"attach_markdown","field":"content"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var result = reply.Structured();
        result.GetProperty("tool").GetString().ShouldBe("attach_markdown");
        result.GetProperty("field").GetString().ShouldBe("content");
        var payload = result.GetProperty("payload").GetString()!;
        payload.ShouldNotBeNullOrWhiteSpace();
        var url = result.GetProperty("upload_url").GetString()!;
        await using var connection = await OpenDatabaseAsync();
        var baseUrl = (await connection.QuerySingleAsync<string>("SELECT base_url FROM instance_settings")).TrimEnd('/');
        url.ShouldStartWith(baseUrl + "/api/uploads/payloads/");
        new Uri(url).Segments[^1].ShouldNotBe(payload);
        result.GetProperty("expires_at").GetDateTimeOffset().ShouldBeInRange(now.AddMinutes(9), now.AddMinutes(11));
        var command = $"curl -T <file> '{url}'";
        result.GetProperty("command").GetString().ShouldBe(command);
        result.GetProperty("project").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        result.GetProperty("open_url").GetString()!.TrimEnd('/').ShouldBe(baseUrl);
        reply.ResultText().ShouldBe($"Nothing is stored yet. Run this in the shell within 10 minutes, then call attach_markdown with payload: \"{payload}\": {command}");
    }

    [Test]
    public async Task CreatePayloadUpload_WhenTokenLacksWrite_ShouldAskForWrite()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "create_payload_upload", """{"tool":"capture_plan"}""");

        reply.Status.ShouldBe(HttpStatusCode.Forbidden);
        reply.WwwAuthenticate.ShouldContain("insufficient_scope");
        reply.WwwAuthenticate.ShouldContain("shiplean:write");
    }
}
