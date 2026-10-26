namespace ShipLean.Contracts.Projects;

// WorkingDays are ISO weekday numbers (1 = Monday .. 7 = Sunday), ascending. Role is the caller's role.
public sealed record ProjectDto(
    Guid Id, string Key, string Name, string Description, ProjectRole Role, int MemberCount,
    IReadOnlyList<int> WorkingDays, int VelocityWindow, decimal? VelocityOverride, int NextItemNumber);
