namespace ShipLean.Application.Features.Retros.Store;

// When a work item completed and the sprint it sat in at that moment (null: none), which can be a sprint that had already ended.
public sealed record RetroItemCompletion(Guid ItemId, DateTimeOffset CompletedAt, Guid? SprintId);
