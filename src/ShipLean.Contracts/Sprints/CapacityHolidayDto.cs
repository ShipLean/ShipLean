namespace ShipLean.Contracts.Sprints;

// A public holiday inside a sprint, as the capacity screen shows it. Label is the server-formatted day ("Thu 25 Dec"): the SPA builds no Date objects.
public sealed record CapacityHolidayDto(DateOnly Date, string Name, string Label);
