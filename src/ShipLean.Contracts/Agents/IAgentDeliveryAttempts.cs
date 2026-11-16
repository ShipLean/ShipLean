namespace ShipLean.Contracts.Agents;

// agent_delivery_attempt: one row per send of an agent delivery, kept, never overwritten.
public interface IAgentDeliveryAttempts
{
    // Inserts ON CONFLICT (delivery_id, attempt) DO NOTHING, because a resend after a failed sent-mark reuses the attempt number.
    // Never throws to the sender: a failure is logged and the send carries on.
    Task RecordAsync(AgentDeliveryAttemptCreate attempt, CancellationToken cancellationToken);

    // Every attempt of the given sessions' deliveries, keyed by session, oldest first.
    Task<ILookup<Guid, AgentDeliveryAttemptDto>> ForSessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken);
}
