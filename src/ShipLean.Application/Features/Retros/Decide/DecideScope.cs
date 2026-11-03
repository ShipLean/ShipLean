namespace ShipLean.Application.Features.Retros.Decide;

// Exactly one scope: a Feature (its own retro plus its stories' and bugs'), a sprint, or a date range (UTC days, both ends included).
public sealed record DecideScope(Guid? FeatureId, Guid? SprintId, DateOnly? From, DateOnly? To);
