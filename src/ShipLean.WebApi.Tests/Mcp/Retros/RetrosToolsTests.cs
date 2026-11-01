using System.Net;
using System.Reflection;
using System.Text.Json;
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
    public async Task ToolsList_WhenListed_ShouldAdvertiseRootCauseOnlyOnWentBadly()
    {
        var token = await TokenAsync(TestUser.Mei, ReadOnly);

        var reply = await PostAsync(token, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        var tool = reply.Json().GetProperty("result").GetProperty("tools").EnumerateArray().Single(t => t.GetProperty("name").GetString() == "log_retrospective");
        var schema = tool.GetProperty("inputSchema");
        var properties = schema.GetProperty("properties");
        PropertyNames(Resolve(properties.GetProperty("went_badly").GetProperty("items"), schema)).ShouldContain("root_cause");
        foreach (var name in new[] { "went_well", "improve", "not_in_plan" })
        {
            var names = PropertyNames(Resolve(properties.GetProperty(name).GetProperty("items"), schema));
            names.ShouldBe(["text", "theme"], ignoreOrder: true);
        }
    }

    private static string[] PropertyNames(JsonElement schema)
        => [.. schema.GetProperty("properties").EnumerateObject().Select(p => p.Name)];

    // The generator may point at a definition with $ref; follow it.
    private static JsonElement Resolve(JsonElement node, JsonElement root)
    {
        if (!node.TryGetProperty("$ref", out var reference))
        {
            return node;
        }

        var current = root;
        foreach (var part in reference.GetString()!.TrimStart('#', '/').Split('/'))
        {
            current = current.GetProperty(part);
        }

        return current;
    }

    [Test]
    public async Task LogRetrospective_WhenTokenIsReadOnly_ShouldBeForbidden()
    {
        var token = await TokenAsync(TestUser.Mei, ReadOnly);

        var reply = await CallToolAsync(token, "log_retrospective", """{"item":"SHIP-90"}""");

        reply.Status.ShouldBe(HttpStatusCode.Forbidden);
    }
}
