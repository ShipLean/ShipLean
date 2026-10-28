namespace ShipLean.Contracts.Projects;

// The agent connection, repository and base branch a project dispatches with. No access check: the caller is dispatch code.
public interface IProjectAgentSettings
{
    // Throws ApiException.NotFound when there is no such project.
    Task<ProjectAgentSettings> GetAsync(Guid projectId, CancellationToken cancellationToken = default);
}
