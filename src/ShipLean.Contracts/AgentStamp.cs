namespace ShipLean.Contracts;

// The one place the string stored in revision.agent_name and comment.agent_name is computed:
// "Claude Code (claude.ai)" for a verified client, "Claude Code (unverified)" otherwise, null when no agent is acting.
public static class AgentStamp
{
    public static string? Of(ICurrentAgent? agent)
    {
        if (agent?.Name is not { } name || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var verified = agent.Verified && !string.IsNullOrWhiteSpace(agent.Host);
        return $"{name} ({(verified ? agent.Host : "unverified")})";
    }
}
