namespace ShipLean.WebApi.Features.Mcp.Tools.Agents;

// One agent_session row as MCP reads it, before the viewer's role decides what of its end they may see.
public sealed record AgentSessionRow(
    Guid Id,
    string Connection,
    string State,
    DateTimeOffset Started,
    DateTimeOffset? Ended,
    string? EndReason,
    string? EndDetail);
