using System.Net;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Gate;

// A POST the gate cannot read as one valid JSON-RPC message never reaches the SDK.
[TestFixture]
public class McpGateMalformedBodyTests : McpFixture
{
    [TestCase("this is not json")]
    [TestCase("")]
    [TestCase("{}")]
    [TestCase("""{"jsonrpc":"2.0","id":1,"method":"tools/call"}""")]
    [TestCase("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{}}""")]
    [TestCase("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":7}}""")]
    [TestCase("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"probe_write",}}""")]
    [TestCase("""{"jsonrpc":"2.0","id":1,"method":"tools/call",/*x*/"params":{"name":"probe_write"}}""")]
    public async Task Mcp_WhenBodyIsNotAValidMessage_ShouldAnswer400(string body)
    {
        var token = await TokenAsync(TestUser.Mei, ReadWritePlan);

        var reply = await PostAsync(token, body);

        reply.Status.ShouldBe(HttpStatusCode.BadRequest);
        Log.Runs("Write").ShouldBe(0);
    }
}
