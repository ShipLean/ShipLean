using System.Net;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Scopes;

// A call without its tool's scope is a 403 step-up before the tool runs; a preview needs only read.
[TestFixture]
public class McpScopeGateTests : McpFixture
{
    private const string MetadataUrl = "https://shiplean.larkspur.dev/.well-known/oauth-protected-resource/mcp";

    [Test]
    public async Task Mcp_WhenScopeMissing_ShouldAnswer403WithUnionOfScopes()
    {
        var token = await TokenAsync(TestUser.Mei, ReadOnly);

        var reply = await CallToolAsync(token, "probe_write");

        reply.Status.ShouldBe(HttpStatusCode.Forbidden);
        reply.WwwAuthenticate.ShouldBe($"Bearer error=\"insufficient_scope\", scope=\"shiplean:read shiplean:write\", resource_metadata=\"{MetadataUrl}\"");
        Log.Runs("Write").ShouldBe(0);
    }

    [Test]
    public async Task Mcp_WhenPlanScopeMissing_ShouldNameAllThreeSorted()
    {
        var token = await TokenAsync(TestUser.Mei, ReadWrite);

        var reply = await CallToolAsync(token, "probe_plan", """{"plan":"abc"}""");

        reply.Status.ShouldBe(HttpStatusCode.Forbidden);
        reply.WwwAuthenticate.ShouldContain("scope=\"shiplean:plan shiplean:read shiplean:write\"");
        Log.Runs("Plan").ShouldBe(0);
    }

    [Test]
    public async Task Mcp_WhenPreviewWithReadOnlyToken_ShouldRunTool()
    {
        var token = await TokenAsync(TestUser.Priya, ReadOnly);

        var reply = await CallToolAsync(token, "probe_plan");

        reply.Status.ShouldBe(HttpStatusCode.OK);
        reply.ResultText().ShouldBe("preview");
    }

    [Test]
    public async Task Mcp_WhenPlanArgumentIsNull_ShouldCountAsPreview()
    {
        var token = await TokenAsync(TestUser.Priya, ReadOnly);

        var reply = await CallToolAsync(token, "probe_plan", """{"plan":null}""");

        reply.Status.ShouldBe(HttpStatusCode.OK);
        reply.ResultText().ShouldBe("preview");
    }

    [Test]
    public async Task Mcp_WhenPlanGivenWithPlanScope_ShouldApply()
    {
        var token = await TokenAsync(TestUser.Priya, ReadWritePlan);

        var reply = await CallToolAsync(token, "probe_plan", """{"plan":"abc"}""");

        reply.ResultText().ShouldBe("applied");
    }

    [Test]
    public async Task Mcp_WhenScopeIsOnTheType_ShouldApplyToItsTools()
    {
        var readOnly = await TokenAsync(TestUser.Tom, ReadOnly);
        var writer = await TokenAsync(TestUser.Tom, ReadWrite);

        var refused = await CallToolAsync(readOnly, "probe_type_write");
        var allowed = await CallToolAsync(writer, "probe_type_write");

        refused.Status.ShouldBe(HttpStatusCode.Forbidden);
        allowed.Status.ShouldBe(HttpStatusCode.OK);
        Log.Runs("TypeWrite").ShouldBe(1);
    }

    [Test]
    public async Task Mcp_WhenMethodScopeDiffersFromType_ShouldUseTheMethods()
    {
        var token = await TokenAsync(TestUser.Tom, ReadOnly);

        var reply = await CallToolAsync(token, "probe_method_read");

        reply.Status.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Mcp_WhenToolUnknown_ShouldLeaveTheAnswerToTheSdk()
    {
        var token = await TokenAsync(TestUser.Tom, ReadOnly);

        var reply = await CallToolAsync(token, "no_such_tool");

        reply.Status.ShouldNotBe(HttpStatusCode.Forbidden);
        ((int)reply.Status).ShouldBeLessThan(500);
        reply.Body.ShouldContain("no_such_tool");
    }

    [Test]
    public async Task Mcp_WhenNotAToolCall_ShouldNotNeedAScope()
    {
        var token = await TokenAsync(TestUser.Tom, ReadOnly);

        var reply = await PostAsync(token, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        reply.Status.ShouldBe(HttpStatusCode.OK);
        reply.Body.ShouldContain("probe_write");
    }

    [Test]
    public async Task Mcp_WhenOneItemWithoutPlanAndReadOnlyToken_ShouldAnswer403StepUp()
    {
        var token = await TokenAsync(TestUser.Tom, ReadOnly);

        var reply = await CallToolAsync(token, "probe_many", """{"items":["a"]}""");

        reply.Status.ShouldBe(HttpStatusCode.Forbidden);
        reply.WwwAuthenticate.ShouldContain("insufficient_scope");
        Log.Runs("Many").ShouldBe(0);
    }

    [Test]
    public async Task Mcp_WhenManyItemsWithoutPlanAndReadOnlyToken_ShouldRunAsPreview()
    {
        var token = await TokenAsync(TestUser.Tom, ReadOnly);

        var reply = await CallToolAsync(token, "probe_many", """{"items":["a","b"]}""");

        reply.Status.ShouldBe(HttpStatusCode.OK);
        reply.ResultText().ShouldBe("preview");
    }

    [Test]
    public async Task Mcp_WhenOneItemWithoutPlanOverLimit_ShouldCountAsWriteAndAnswer429From121st()
    {
        var token = await TokenAsync(TestUser.Mei, ReadWrite);

        var statuses = await PostRepeatedlyAsync(token, """{"items":["a"]}""");

        statuses.Take(120).ShouldAllBe(status => status == HttpStatusCode.OK);
        statuses[120].ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task Mcp_WhenManyItemsWithoutPlanOverLimit_ShouldNotCountAsWrite()
    {
        var token = await TokenAsync(TestUser.Priya, ReadWrite);

        var statuses = await PostRepeatedlyAsync(token, """{"items":["a","b"]}""");

        statuses.ShouldAllBe(status => status == HttpStatusCode.OK);
    }

    private async Task<List<HttpStatusCode>> PostRepeatedlyAsync(string token, string arguments)
    {
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 121; i++)
        {
            statuses.Add((await CallToolAsync(token, "probe_many", arguments)).Status);
        }
        return statuses;
    }
}
