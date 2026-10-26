using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.Status;

// PART is the seeded project Joel is not in. No PART item is seeded, and none is needed: a key in a project the caller is
// not in answers exactly like an unknown one.
public sealed class StatusMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("sprint_status", McpScopes.Read, ProjectRole.Member, """{"project":"PART","sprint":"current"}""", ReadOnly: true),
        new("whats_changed_since", McpScopes.Read, ProjectRole.Member, """{"project":"PART","since":"2020-01-01T00:00:00Z"}""", ReadOnly: true),
        new("feature_progress", McpScopes.Read, ProjectRole.Member, """{"item":"PART-1"}""", ReadOnly: true),
    ];
}
