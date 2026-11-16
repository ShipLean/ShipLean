namespace ShipLean.Contracts.Agents;

// What an agent session does (agent_session.mode, D9): implement its item (open a PR), propose (read the repository
// read-only and answer with a proposal for a GitHub issue), or demo (make a sprint demo video with the relay's demo tool profile and
// upload it). Stored lowercase: 'implement', 'propose' or 'demo'.
public enum AgentSessionMode
{
    Implement,
    Propose,
    Demo,
}
