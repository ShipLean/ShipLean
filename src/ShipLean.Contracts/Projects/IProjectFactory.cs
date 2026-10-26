namespace ShipLean.Contracts.Projects;

// Creates projects. Callers check the creator's access to the copy-from project first; the factory itself enforces the
// instance's project_creation rule, so every caller (the new-project screen, an import's new project) gets it.
public interface IProjectFactory
{
    // The creator becomes Admin; states, boards and the optional Sprint 1 are created in one transaction. Throws ApiException.Conflict (key taken), ApiException.Validation (invalid key, blank name, no working day, bad sprint dates), ApiException.NotFound (unknown copy-from project or member email), ApiException.Forbidden (project_creation is admins and the creator is not an active instance admin).
    Task<ProjectDto> CreateAsync(ProjectCreate create, CancellationToken cancellationToken = default);

    // Never throws for a bad key: the answer says why (taken, takenByAnotherProject, invalid) and offers alternatives.
    Task<KeyCheckDto> CheckKeyAsync(string key, string name, CancellationToken cancellationToken = default);
}
