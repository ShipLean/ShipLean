namespace ShipLean.Contracts.Agents;

// How long after a session ended its connection can still reach the item (reads, comments): an agent finishing up after its response.
public static class AgentSessionAccess
{
    public static readonly TimeSpan EndedWindow = TimeSpan.FromHours(24);
}
