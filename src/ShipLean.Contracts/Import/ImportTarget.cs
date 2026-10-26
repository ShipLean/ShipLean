namespace ShipLean.Contracts.Import;

// New project (Key and Name) or an existing empty project (ProjectId); the unused fields are null.
public sealed record ImportTarget(ImportTargetKind Kind, string? Key, string? Name, Guid? ProjectId);
