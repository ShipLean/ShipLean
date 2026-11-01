using System.Net;
using System.Reflection;
using ModelContextProtocol.Server;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Retros;

[TestFixture]
public class RetrosToolsTests : McpFixture
{
    private static MethodInfo Tool(string name)
        => typeof(ApiMarker).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Single(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name == name);

    [TestCase("list_retrospectives", McpScopes.Read)]
    [TestCase("log_retrospective", McpScopes.Write)]
    public void Tool_WhenRegistered_ShouldCarryItsScope(string name, string scope)
    {
        Tool(name).GetCustomAttribute<McpScopeAttribute>()!.Scope.ShouldBe(scope);
    }

    [Test]
    public async Task ToolsList_WhenListed_ShouldIncludeBothRetroTools()
    {
        var token = await TokenAsync(TestUser.Mei, ReadOnly);

        var reply = await PostAsync(token, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        reply.Body.ShouldContain("log_retrospective");
        reply.Body.ShouldContain("list_retrospectives");
    }

    [Test]
    public async Task LogRetrospective_WhenTokenIsReadOnly_ShouldBeForbidden()
    {
        var token = await TokenAsync(TestUser.Mei, ReadOnly);

        var reply = await CallToolAsync(token, "log_retrospective", """{"item":"SHIP-90"}""");

        reply.Status.ShouldBe(HttpStatusCode.Forbidden);
    }
}
