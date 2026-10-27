namespace ShipLean.Contracts.Agents;

// What a live transfer token lets its holder do: AttachmentId is set for a download. UserId is who asked for the URL,
// AgentStamp the agent as it was then (AgentStamp.Of), ContentType what the agent declared.
public sealed record TransferGrant(
    TransferKind Kind,
    Guid WorkItemId,
    Guid? AttachmentId,
    string Name,
    string ContentType,
    Guid UserId,
    string AgentStamp,
    DateTimeOffset ExpiresAt);
