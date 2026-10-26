namespace ShipLean.Contracts.Sprints;

public sealed record ForecastDto(IReadOnlyList<ForecastSprintDto> Sprints);
