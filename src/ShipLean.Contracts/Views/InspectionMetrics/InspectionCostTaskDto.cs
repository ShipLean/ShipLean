namespace ShipLean.Contracts.Views.InspectionMetrics;

// One of the range's tasks with the most tokens (CK2). Who is the lead-time Who (the agent that started it, else the person); Agent
// is whether Who is an agent. Models are its runs' models. LeadLabel is its lead time; PeriodId the sprint its lead time ended in.
public sealed record InspectionCostTaskDto(
    string Key,
    string Title,
    string? FeatureKey,
    string Who,
    bool Agent,
    IReadOnlyList<string> Models,
    int Runs,
    long Tokens,
    string TokensLabel,
    string LeadLabel,
    Guid? PeriodId);
