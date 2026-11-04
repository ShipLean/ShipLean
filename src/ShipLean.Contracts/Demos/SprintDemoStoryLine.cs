namespace ShipLean.Contracts.Demos;

public sealed record SprintDemoStoryLine(
    string Key,
    string Type,
    string Title,
    string State,
    decimal? Points,
    string? Summary,
    IReadOnlyList<string> Criteria,
    IReadOnlyList<SprintDemoTaskLine> Tasks);
