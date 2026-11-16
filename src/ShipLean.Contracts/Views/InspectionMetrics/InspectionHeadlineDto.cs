namespace ShipLean.Contracts.Views.InspectionMetrics;

// What the cards headline (item D1): the last finished sprint, the running sprint so far, this calendar quarter to date and last
// calendar quarter, whatever the range shows (the window always reads the quarter before). Each is null when there is none.
public sealed record InspectionHeadlineDto(
    InspectionHeadlinePeriodDto? Finished,
    InspectionHeadlinePeriodDto? SoFar,
    InspectionQuarterDto? QuarterToDate,
    InspectionQuarterDto? LastQuarter);
