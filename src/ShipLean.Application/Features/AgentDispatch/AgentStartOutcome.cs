using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.AgentDispatch;

// Block is null when SessionId was opened.
public sealed record AgentStartOutcome(Guid? SessionId, AgentStartBlock? Block);
