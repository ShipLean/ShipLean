using System.Data;
using Dapper;
using ModelContextProtocol.Server;
using Npgsql;
using ShipLean.WebApi.Features.Mcp.Contracts;

// Tool-shaped types that break (or keep) each MCP rule on purpose. They live in the test assembly, under a namespace the
// production rules do not scan, so each rule can be shown to fail on a violation. Never register these as tools.
namespace ShipLean.UnitTest.Architecture.Mcp.Fixtures.Bad
{
    [McpServerToolType]
    internal sealed class UnscopedTools
    {
        [McpServerTool]
        public string NoScope() => "x";

        [McpServerTool]
        [McpScope(McpScopes.Read)]
        public string Scoped() => "x";
    }

    [McpServerToolType]
    [McpScope(McpScopes.Write)]
    internal sealed class TypeScopedTools
    {
        [McpServerTool]
        public string Covered() => "x";
    }

    [McpServerToolType]
    [McpScope(McpScopes.Read)]
    public sealed class PublicTools
    {
        [McpServerTool]
        public string Visible() => "x";
    }

    [McpServerToolType]
    [McpScope(McpScopes.Read)]
    internal sealed class DapperTools
    {
        [McpServerTool]
        public Task<int> Reads(IDbConnection connection) => connection.ExecuteAsync("SELECT 1");
    }

    [McpServerToolType]
    [McpScope(McpScopes.Read)]
    internal sealed class NpgsqlTools
    {
        public NpgsqlDataSource? Source { get; set; }

        [McpServerTool]
        public string Reads() => "x";
    }
}
