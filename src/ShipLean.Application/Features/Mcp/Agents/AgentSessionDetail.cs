using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.Mcp.Agents;

// One session as get_agent_session reads it, before the viewer's role decides what of its end, run log and delivery attempts
// they may see (AgentSessionVisibility.Apply). Activities are newest first; NextBefore is the cursor for the next page, null on the last.
public sealed record AgentSessionDetail(
    AgentSessionRow Session,
    string ItemKey,
    AgentRunLogDto? RunLog,
    IReadOnlyList<AgentSessionDetail.Activity> Activities,
    string? NextBefore,
    IReadOnlyList<AgentDeliveryAttemptDto> Deliveries)
{
    // Every text is already capped (AgentTextCap); Truncated says any of them was cut.
    public sealed record Activity(Guid Id, string Type, string? Body, string? Action, string? Parameter, string? Result, bool Truncated, DateTimeOffset At);
}
