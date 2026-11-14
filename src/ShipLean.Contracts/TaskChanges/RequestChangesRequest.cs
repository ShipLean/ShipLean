namespace ShipLean.Contracts.TaskChanges;

public sealed record RequestChangesRequest(string? Note, bool KeepAssignee);
