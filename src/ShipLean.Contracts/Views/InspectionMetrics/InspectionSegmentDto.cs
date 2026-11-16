namespace ShipLean.Contracts.Views.InspectionMetrics;

// A stretch of the selected task's time. Counted is false only for the unresolved tail.
public sealed record InspectionSegmentDto(
    InspectionSegmentKind Kind,
    DateTimeOffset From,
    DateTimeOffset To,
    double FromHours,
    double Hours,
    string Label,
    bool Counted);
