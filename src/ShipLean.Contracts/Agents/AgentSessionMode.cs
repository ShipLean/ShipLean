namespace ShipLean.Contracts.Agents;

// What an agent session does (agent_session.mode, D9): implement its item (open a PR), or propose (read the repository
// read-only and answer with a proposal for a GitHub issue). Stored lowercase: 'implement' or 'propose'.
public enum AgentSessionMode
{
    Implement,
    Propose,
}
