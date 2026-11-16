namespace ShipLean.Contracts.Views.InspectionMetrics;

// A task's dot on the chart. OverTwoDays dots are pinned to the top edge in red; Longest is the one labelled.
public sealed record InspectionDotDto(string TaskKey, double LeadHours, string LeadLabel, bool OverTwoDays, bool Longest);
