namespace ShipLean.Contracts.Views.InspectionMetrics;

// Tokens per task from the agent runs (SHIP-1398 CK2, design "Cost"): tokens only, never money (SHIP-1463 D21). The view's Cost is
// null when Inspection (agent runs) is off for the instance or the project. Available is false, with a Note saying why, when the
// store cannot total tokens per task (Tempo + Loki, or unreachable); its lists are then empty and its range and headline stats
// null. Periods follow the view's periods and Quarters its quarters, by id and label. Range is the shown sprints together.
// MostTokens is the range's 8 tasks with the most tokens, most first. ByFeature totals the range's tasks with runs by Feature (Key
// null for tasks with no Feature), most tokens first.
public sealed record InspectionCostDto(
    bool Available,
    string? Note,
    IReadOnlyList<InspectionPeriodCostDto> Periods,
    IReadOnlyList<InspectionQuarterCostDto> Quarters,
    InspectionCostStatsDto? Range,
    InspectionCostHeadlineDto Headline,
    IReadOnlyList<InspectionCostTaskDto> MostTokens,
    IReadOnlyList<InspectionCostFeatureDto> ByFeature);
