using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Sprints;

namespace ShipLean.Contracts.Views;

// Level is Stories, Features or Epics. List and Tree fill Rows (Sections empty); Planning fills Sections (Rows empty).
// TagColours are the matching board's. Forecast and Velocity are the sprint contracts' shapes, null unless asked for.
public sealed record BacklogViewDto(
    Guid ProjectId,
    BoardLevel Level,
    BacklogMode Mode,
    int TotalCount,
    IReadOnlyList<BacklogRowDto> Rows,
    IReadOnlyList<BacklogSectionDto> Sections,
    IReadOnlyList<TagColourJson> TagColours,
    ForecastDto? Forecast,
    VelocityDto? Velocity);
