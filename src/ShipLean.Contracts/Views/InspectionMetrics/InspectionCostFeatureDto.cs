namespace ShipLean.Contracts.Views.InspectionMetrics;

// The range's tokens of one Feature (CK2): Key null and Title "No Feature" for tasks with none; Tasks counts its tasks with runs.
public sealed record InspectionCostFeatureDto(string? Key, string Title, int Tasks, long Tokens, string TokensLabel);
