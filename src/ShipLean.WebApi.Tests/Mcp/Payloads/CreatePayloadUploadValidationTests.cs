using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Payloads;

// create_payload_upload refuses a tool that takes no payload and a field that is not one of the tool's text arguments,
// naming what would work, and issues nothing when it refuses.
[TestFixture]
public class CreatePayloadUploadValidationTests : McpFixture
{
    protected override bool WithProbeTools => false;

    [Test]
    public async Task CreatePayloadUpload_WhenToolTakesNoPayload_ShouldListTheToolsThatDo()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var before = await CountPayloadsAsync();

        var reply = await CallToolAsync(token, "create_payload_upload", """{"tool":"sprint_status"}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("field").GetString().ShouldBe("tool");
        var message = error.GetProperty("message").GetString()!;
        message.ShouldStartWith("sprint_status takes no payload. These do: ");
        message.ShouldContain("capture_plan");
        message.ShouldContain("attach_markdown");
        (await CountPayloadsAsync()).ShouldBe(before);
    }

    [Test]
    public async Task CreatePayloadUpload_WhenFieldIsNotTextArgument_ShouldListTheFields()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var before = await CountPayloadsAsync();

        var reply = await CallToolAsync(token, "create_payload_upload", """{"tool":"attach_markdown","field":"project"}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("field").GetString().ShouldBe("field");
        var message = error.GetProperty("message").GetString()!;
        message.ShouldStartWith("project is not a text argument of attach_markdown. These are: ");
        message.ShouldContain("content");
        (await CountPayloadsAsync()).ShouldBe(before);
    }

    private async Task<int> CountPayloadsAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM tool_payload");
    }
}
