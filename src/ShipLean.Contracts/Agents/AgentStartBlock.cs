namespace ShipLean.Contracts.Agents;

// Why an agent session cannot start on an item right now (camelCase on the wire).
// NoConnection: the project has no agent connection, or the one it has is archived. NoRepository: the project has no repository URL.
// LiveSession: the item already has a session that is submitted, working or waiting for input.
public enum AgentStartBlock
{
    NoConnection,
    NoRepository,
    LiveSession,
}
