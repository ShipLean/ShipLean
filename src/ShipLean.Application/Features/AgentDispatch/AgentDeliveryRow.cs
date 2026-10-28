namespace ShipLean.Application.Features.AgentDispatch;

// A claimed outbox row with what the sender needs of its session.
internal sealed record AgentDeliveryRow(Guid Id, Guid SessionId, int Seq, string Action, string Body, int Attempts, Guid ConnectionId, string SessionState);
