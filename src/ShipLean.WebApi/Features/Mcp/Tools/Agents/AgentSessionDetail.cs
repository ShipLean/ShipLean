using ShipLean.Application.Features.Mcp.Agents;
using ShipLean.Contracts.Agents;

namespace ShipLean.WebApi.Features.Mcp.Tools.Agents;

// One session as get_agent_session reads it, before the viewer's role decides what of its end, run log and delivery attempts
// they may see (AgentSessionVisibility.Apply). Activities are newest first; NextBefore is the cursor for the next page.
public sealed record AgentSessionDetail(
    AgentSessionRow Session,
    string ItemKey,
    AgentRunLogDto? RunLog,
    IReadOnlyList<AgentSessionDetail.Activity> Activities,
    string? NextBefore,
    IReadOnlyList<AgentDeliveryAttemptDto> Deliveries)
{
    public sealed record Activity(Guid Id, string Type, string? Body, string? Action, string? Parameter, string? Result, bool Truncated, DateTimeOffset At);
}
