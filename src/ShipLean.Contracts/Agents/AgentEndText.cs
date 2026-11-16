namespace ShipLean.Contracts.Agents;

// The one sentence that says why a session ended, for the agent panel, the Demo band and MCP. Detail is what the viewer may
// see (AgentSessionVisibility), so a null detail leaves it out.
public static class AgentEndText
{
    // TODO: stub sentence; replaced by the per-reason wording in contract C2 (deliveryFailed, agentError, stale, ...).
    public static string For(string endReason, string? detail) => $"The agent session ended ({endReason}).";
}
