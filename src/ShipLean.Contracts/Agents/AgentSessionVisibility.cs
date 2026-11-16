namespace ShipLean.Contracts.Agents;

// Who sees what of a session's end (C3), the one place the rule lives: the panel, the demo reader and MCP all call it.
// Every member sees the end reason and its sentence. A deliveryFailed detail and its delivery attempts are for project admins
// only, because a receiver's error can name the relay's host or port; any other detail is already in the member-visible
// activity log. The run log is for admins only, for any reason: raw agent output can hold hosts or secrets.
public static class AgentSessionVisibility
{
    public static AgentSessionEndView Apply(string? endReason, string? endDetail, AgentRunLogDto? runLog, ProjectRole role)
    {
        var admin = role == ProjectRole.Admin;
        if (endReason is null)
        {
            return new AgentSessionEndView(null, null, null, null, ShowDeliveries: true);
        }

        var adminOnly = endReason == AgentEndReasons.DeliveryFailed;
        var detail = admin || !adminOnly ? endDetail : null;
        return new AgentSessionEndView(endReason, AgentEndText.For(endReason, detail), detail, admin ? runLog : null, ShowDeliveries: admin || !adminOnly);
    }
}
