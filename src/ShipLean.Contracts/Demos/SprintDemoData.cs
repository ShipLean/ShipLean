namespace ShipLean.Contracts.Demos;

public sealed record SprintDemoData(
    SprintDemoSprint Sprint,
    SprintDemoProject Project,
    IReadOnlyList<SprintDemoFeatureData> Features,
    IReadOnlyList<SprintDemoStoryLine> OtherWork,
    SprintDemoRetroData SprintRetro,
    IReadOnlyList<SprintDemoActionLine> ActionItems,
    bool Truncated);
