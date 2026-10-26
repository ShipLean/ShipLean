using System.Net;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Gate;

// A write counts against the per-user limit however the request spells it.
[TestFixture]
public class McpGateWriteLimitTests : McpFixture
{
    private const string Capitalised = """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"Name":"probe_write"}}""";
    private const string WithByteOrderMark = "\uFEFF{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"probe_write\"}}";

    [Test]
    public async Task Mcp_WhenWritesSpelledWithCapitalNameOverLimit_ShouldAnswer429From121st()
    {
        var statuses = await PostRepeatedlyAsync(TestUser.Mei, Capitalised);

        statuses.Take(120).ShouldAllBe(status => status == HttpStatusCode.OK);
        statuses[120].ShouldBe(HttpStatusCode.TooManyRequests);
        Log.Runs("Write").ShouldBe(120);
    }

    [Test]
    public async Task Mcp_WhenWritesStartWithByteOrderMarkOverLimit_ShouldAnswer429From121st()
    {
        var statuses = await PostRepeatedlyAsync(TestUser.Priya, WithByteOrderMark);

        statuses.Take(120).ShouldAllBe(status => status == HttpStatusCode.OK);
        statuses[120].ShouldBe(HttpStatusCode.TooManyRequests);
        Log.Runs("Write").ShouldBe(120);
    }

    private async Task<List<HttpStatusCode>> PostRepeatedlyAsync(TestUser user, string body)
    {
        var token = await TokenAsync(user, ReadWritePlan);
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 121; i++)
        {
            statuses.Add((await PostAsync(token, body)).Status);
        }
        return statuses;
    }
}
