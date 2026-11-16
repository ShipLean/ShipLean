namespace ShipLean.Contracts.Views.InspectionMetrics;

// A neighbouring sprint, for the period header's previous and next buttons ("‹ S22", "S24 ›").
public sealed record InspectionPeriodRefDto(Guid Id, string ShortName);
