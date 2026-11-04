namespace ShipLean.Contracts.Demos;

public sealed record SprintDemoFeatureData(
    string Key,
    string Title,
    string State,
    string Url,
    string Goal,
    string? Why,
    string? Measure,
    IReadOnlyList<SprintDemoStoryLine> Stories,
    SprintDemoPlanFacts? Plan,
    SprintDemoRetroData Retro,
    IReadOnlyList<string> AttachmentNames,
    IReadOnlyList<string> PullRequests);
