namespace ShipLean.Contracts.TaskChanges.Dtos;

public sealed record RequestTaskChangesRequest(string? Note, bool KeepAssignee);
