using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Views.Search;

// Url is the sprint's taskboard, a relative SPA path. Status and DatesLabel are as on SprintDto.
public sealed record SearchSprintDto(
    string Name,
    string Goal,
    DateOnly StartDate,
    DateOnly EndDate,
    SprintStatus Status,
    string DatesLabel,
    string ProjectKey,
    string Url);
