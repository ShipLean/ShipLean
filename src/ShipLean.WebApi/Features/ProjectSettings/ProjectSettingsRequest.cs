using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;

namespace ShipLean.WebApi.Features.ProjectSettings;

// The body of PUT api/projects/{id}/settings. It is ProjectSettingsPut, except General may carry the key the page loaded:
// the same key is accepted, a different one is refused (a key never changes).
public sealed record ProjectSettingsRequest(
    ProjectGeneralRequest General, IReadOnlyList<MemberPut> Members, ProjectForecastSettings Sprints, StateChanges States)
{
    public string? Key => General?.Key;

    // A section left out of the body is a validation error, never a null for the service to trip on.
    public ProjectSettingsPut ToPut()
        => General is null || Members is null || Sprints is null || States is null
            ? throw ApiException.Validation("The settings need General, Members, Sprints and States.")
            : new ProjectSettingsPut(new ProjectGeneralPut(General.Name, General.Description, General.WorkingDays, General.AgentConnectionId, General.RepoUrl, General.RepoBaseBranch, General.Provided), Members, Sprints, States);
}
