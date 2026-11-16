using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.InspectionMetrics;

public sealed class BackfillCommitTimesMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("backfill_commit_times", McpScopes.Write, ProjectRole.Member,
            NoProject: "Names no project: it works on commit links across the install and only an instance admin may call it; BackfillCommitTimesToolTests checks that anyone else gets not_found."),
    ];
}
