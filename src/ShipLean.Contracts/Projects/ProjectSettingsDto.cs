namespace ShipLean.Contracts.Projects;

// The settings page as one document: General, Members, Sprints & forecast, States.
public sealed record ProjectSettingsDto(
    ProjectGeneralSettings General, IReadOnlyList<MemberDto> Members, ProjectForecastSettings Sprints, IReadOnlyList<StateDto> States);
