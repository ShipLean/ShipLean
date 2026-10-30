namespace ShipLean.Contracts.Sprints;

// Members lists only the entered rows (a member with no entry is absent, and the sprint falls back to velocity); TeamTotal is the sum of their Points.
// Velocity is the stored value (null on an old points-only sprint); VelocityDefault is what a new entry starts from: the latest earlier
// sprint's velocity, else the project's override, else the average, rounded to 1 decimal. WorkingDays (W) counts the sprint's dates on the
// project's working weekdays with holidays kept in; Holidays are the country's holidays on those days; DefaultDays = W - Holidays.Count.
// Ended sprints are read-only. A member's points are velocity / members * days / W.
public sealed record SprintCapacityDto(
    Guid SprintId,
    IReadOnlyList<MemberCapacityDto> Members,
    decimal TeamTotal,
    decimal? Velocity,
    decimal VelocityDefault,
    int WorkingDays,
    int DefaultDays,
    IReadOnlyList<CapacityHolidayDto> Holidays,
    string? HolidayCountry,
    bool Ended);
