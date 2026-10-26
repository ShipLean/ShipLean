namespace ShipLean.WebApi.Features.Mcp.Planning.Reports;

// How a sub-agent's stop ended: done, blocked or partial. The wire word is the lowercase name.
internal enum ReportStatus
{
    Done,
    Blocked,
    Partial,
}
