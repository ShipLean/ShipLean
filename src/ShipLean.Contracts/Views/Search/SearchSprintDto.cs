namespace ShipLean.Contracts.Views.Search;

// Url is the sprint's taskboard, a relative SPA path.
public sealed record SearchSprintDto(string Name, string Goal, DateOnly StartDate, DateOnly EndDate, string ProjectKey, string Url);
