using ShipLean.Contracts.Agents;

namespace ShipLean.Contracts.Demos;

public sealed record DemoAgentCheckDto(bool Ready, AgentStartBlock? Block, string Message);
