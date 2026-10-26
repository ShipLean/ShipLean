using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// Test-only tools, registered through ConfigureTestServices (McpFixture). They stand in for Lane M's tools: each carries
// the scope and throws the error its name says, and every run is counted so a test can show a refused call never ran.
[McpServerToolType]
public sealed class ProbeTools(ProbeLog log)
{
    [McpServerTool(Name = "probe_whoami"), McpScope(McpScopes.Read)]
    public string Whoami(ICurrentUser user, ICurrentAgent agent)
    {
        log.Count(nameof(Whoami));
        return $"{user.Id}|{agent.Name}|{agent.Host}|{agent.Verified}";
    }

    [McpServerTool(Name = "probe_write"), McpScope(McpScopes.Write)]
    public string Write()
    {
        log.Count(nameof(Write));
        return "written";
    }

    [McpServerTool(Name = "probe_plan"), McpScope(McpScopes.Plan, PreviewScope = McpScopes.Read)]
    public string Plan(string? plan = null)
    {
        log.Count(nameof(Plan));
        return plan is null ? "preview" : "applied";
    }

    [McpServerTool(Name = "probe_many"), McpScope(McpScopes.Write, PreviewScope = McpScopes.Read, PreviewWhenManyOf = "items")]
    public string Many(string[]? items = null, string? plan = null)
    {
        log.Count(nameof(Many));
        return plan is null && items?.Length > 1 ? "preview" : "applied";
    }

    [McpServerTool(Name = "probe_not_found"), McpScope(McpScopes.Read)]
    public string NotFound() => throw ApiException.NotFound("There is no SHIP-999 in this project.");

    [McpServerTool(Name = "probe_ambiguous"), McpScope(McpScopes.Read)]
    public string Ambiguous() => throw ApiException.Ambiguous("Sam is two people.", "assignee", ["Sam Lee", "Sam Ito"]);

    [McpServerTool(Name = "probe_explode"), McpScope(McpScopes.Read)]
    public string Explode() => throw new InvalidOperationException("connection string is hunter2");
}

// A scope on the type covers its tools, and a method's own scope wins over it.
[McpServerToolType]
[McpScope(McpScopes.Write)]
public sealed class TypeScopedProbeTools(ProbeLog log)
{
    [McpServerTool(Name = "probe_type_write")]
    public string TypeWrite()
    {
        log.Count(nameof(TypeWrite));
        return "type";
    }

    [McpServerTool(Name = "probe_method_read"), McpScope(McpScopes.Read)]
    public string MethodRead()
    {
        log.Count(nameof(MethodRead));
        return "method";
    }
}

// A tool nobody gave a scope: the gate must fail closed on it, the tool never runs.
[McpServerToolType]
public sealed class UnscopedProbeTools(ProbeLog log)
{
    [McpServerTool(Name = "probe_unscoped")]
    public string Unscoped()
    {
        log.Count(nameof(Unscoped));
        return "ran";
    }
}
