using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Demos;

public sealed record SprintDemoFeatureDto(string Key, string Title, StateRef State, string Url);
