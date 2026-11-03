namespace ShipLean.Contracts.Retros;

// Every filter is optional; Theme is a theme name. Callers have resolved the project, sprint and feature.
public sealed record RetroListRequest(Guid ProjectId, Guid? SprintId, Guid? FeatureId, RetroCategory? Category, string? Theme, DateOnly? Since, bool Undecided, bool NoSprint);
