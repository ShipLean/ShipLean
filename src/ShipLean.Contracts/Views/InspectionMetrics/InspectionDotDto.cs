namespace ShipLean.Contracts.Views.InspectionMetrics;

// A task's dot on the chart; the hover reads "KEY Title · 2.1 d" with "(to Resolved)" when EndedBy is Resolved. OverTwoDays dots
// are pinned to the top edge in red; Longest is the one labelled.
public sealed record InspectionDotDto(
    string TaskKey,
    string Title,
    double LeadHours,
    string LeadLabel,
    LeadTimeEndedBy EndedBy,
    bool OverTwoDays,
    bool Longest);
