namespace ShipLean.Contracts.Views.InspectionMetrics;

// What the cards headline (item D1): the last finished sprint, and the running sprint so far. Either is null when there is none.
public sealed record InspectionHeadlineDto(InspectionHeadlinePeriodDto? Finished, InspectionHeadlinePeriodDto? SoFar);
