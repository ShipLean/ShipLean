namespace ShipLean.Contracts.Agents;

// AuthorizationId is the agent's OAuth authorization, null for a caller that has none (the gateway); revoking it ends unused tokens.
public sealed record NewTransfer(
    TransferKind Kind,
    Guid WorkItemId,
    Guid? AttachmentId,
    string Name,
    string ContentType,
    Guid UserId,
    Guid? AuthorizationId,
    string AgentStamp);
