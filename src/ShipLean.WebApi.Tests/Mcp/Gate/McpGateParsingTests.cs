using System.Net;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Gate;

// The gate reads a body the way the SDK does, so no spelling the SDK accepts gets a read-only token past a write scope.
[TestFixture]
public class McpGateParsingTests : McpFixture
{
    private const string Bom = "﻿";

    [TestCase("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"Name":"probe_write","arguments":{}}}""")]
    [TestCase("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"NAME":"probe_write"}}""")]
    [TestCase("﻿{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"probe_write\"}}")]
    [TestCase("  \r\n\t{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"probe_write\"}}")]
    [TestCase("""{"jsonrpc":"2.0","id":1,"method":"tools\/call","params":{"name":"probe_write"}}""")]
    [TestCase("""{"jsonrpc":"2.0","method":"tools/call","params":{"name":"probe_write"}}""")]
    public async Task Mcp_WhenReadOnlyTokenSpellsAWriteCallUnusually_ShouldRefuseBeforeTheToolRuns(string body)
    {
        var token = await TokenAsync(TestUser.Mei, ReadOnly);

        var reply = await PostAsync(token, body);

        reply.Status.ShouldBe(HttpStatusCode.Forbidden);
        Log.Runs("Write").ShouldBe(0);
    }

    [TestCase("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"probe_plan","Arguments":{"plan":"abc"}}}""")]
    [TestCase("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"probe_plan","arguments":{"plan":"abc"},"arguments":{}}}""")]
    public async Task Mcp_WhenReadOnlyTokenSpellsAPlanUnusually_ShouldNotApplyIt(string body)
    {
        var token = await TokenAsync(TestUser.Mei, ReadOnly);

        var reply = await PostAsync(token, body);

        reply.Body.ShouldNotContain("applied", Case.Sensitive);
    }

    [Test]
    public async Task Mcp_WhenPlanArgumentIsSpelledWithCapital_ShouldBeAPreview()
    {
        var token = await TokenAsync(TestUser.Mei, ReadOnly);

        var reply = await PostAsync(token, """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"probe_plan","arguments":{"Plan":"abc"}}}""");

        reply.ResultText().ShouldBe("preview");
    }

    [Test]
    public async Task Mcp_WhenBodyStartsWithByteOrderMarkAndTokenMayWrite_ShouldRunTheTool()
    {
        var token = await TokenAsync(TestUser.Mei, ReadWrite);

        var reply = await PostAsync(token, Bom + """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"probe_write"}}""");

        reply.Status.ShouldBe(HttpStatusCode.OK);
        Log.Runs("Write").ShouldBe(1);
    }
}
