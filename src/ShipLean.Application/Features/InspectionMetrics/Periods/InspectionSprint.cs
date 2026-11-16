namespace ShipLean.Application.Features.InspectionMetrics.Periods;

// A live sprint of the project, by its first and last day. Its id is the period id on the wire.
public sealed record InspectionSprint(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate);
