namespace ShipLean.Contracts.Projects;

// The settings page as saved: General, the full Members list (a member left out is removed), Sprints & forecast, and the
// States section's changes since it was loaded.
public sealed record ProjectSettingsPut(
    ProjectGeneralPut General, IReadOnlyList<MemberPut> Members, ProjectForecastSettings Sprints, StateChanges States);
