using System.Net;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Errors;

// A tool throws ApiException and never catches it; the caller gets an McpError result, not an HTTP error.
[TestFixture]
public class McpToolErrorTests : McpFixture
{
    [Test]
    public async Task Mcp_WhenToolThrowsNotFound_ShouldReturnMcpError()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "probe_not_found");

        reply.Status.ShouldBe(HttpStatusCode.OK);
        reply.IsToolError().ShouldBeTrue();
        reply.ResultText().ShouldBe("There is no SHIP-999 in this project.");
        var structured = reply.Structured();
        structured.GetProperty("ok").GetBoolean().ShouldBeFalse();
        var error = structured.GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
        error.GetProperty("message").GetString().ShouldBe("There is no SHIP-999 in this project.");
        error.GetProperty("field").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        error.GetProperty("candidates").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
    }

    [Test]
    public async Task Mcp_WhenToolThrowsAmbiguous_ShouldCarryFieldAndCandidates()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var error = (await CallToolAsync(token, "probe_ambiguous")).Structured().GetProperty("error");

        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Ambiguous);
        error.GetProperty("field").GetString().ShouldBe("assignee");
        error.GetProperty("candidates").EnumerateArray().Select(e => e.GetString()).ShouldBe(["Sam Lee", "Sam Ito"]);
    }

    [Test]
    public async Task Mcp_WhenToolThrowsAnythingElse_ShouldReturnInternalErrorWithoutTheDetail()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "probe_explode");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Internal);
        reply.Structured().GetProperty("error").GetProperty("message").GetString().ShouldBe(ApiException.Internal().Message);
        reply.Body.ShouldNotContain("hunter2");
    }

    [Test]
    public async Task Mcp_WhenToolSucceeds_ShouldNotBeAnError()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "probe_whoami");

        reply.IsToolError().ShouldBeFalse();
    }
}
