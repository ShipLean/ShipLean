namespace ShipLean.Contracts.Demos;

// StoriesOmitted is how many of the Feature's sprint stories are not in Stories: cut to the per-Feature cap or to fit the size budget
// (SHIP-2330). A Feature with stories omitted has stories; the data just leaves them out.
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
    IReadOnlyList<string> PullRequests,
    int StoriesOmitted = 0);
