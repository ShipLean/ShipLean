namespace ShipLean.Contracts.Search;

public sealed record SearchResultSprint(Guid Id, string Name, string Goal, DateOnly StartDate, DateOnly EndDate, string ProjectKey);
