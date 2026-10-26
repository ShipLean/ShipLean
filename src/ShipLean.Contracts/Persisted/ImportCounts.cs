namespace ShipLean.Contracts.Persisted;

public sealed record ImportCounts(ImportEntityCount Sprints, ImportEntityCount Issues, ImportEntityCount Comments, ImportEntityCount Attachments, ImportEntityCount Revisions);
