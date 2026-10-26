namespace ShipLean.Contracts;

// The access rule: every command and query that takes a project id, or the id of anything inside a project, calls
// RequireAsync first (before it validates the body) and states the minimum role it needs, so every route shares one
// check. A user with no access at all gets ApiException.NotFound, exactly as for an id that does not exist; a role
// below the minimum gets ApiException.Forbidden. Instance admins have no implicit project access.
public interface IProjectAccess
{
    // Returns the user's effective role in the project. Throws NotFound (not a member, or no such project) and Forbidden (role below minimum).
    Task<ProjectRole> RequireAsync(Guid projectId, ProjectRole minimum, CancellationToken cancellationToken = default);

    // The same for an item inside a project: resolves its project by SQL (archived items still resolve). Throws NotFound (unknown id, an import job with no project yet, or not a member) and Forbidden.
    Task<(Guid ProjectId, ProjectRole Role)> RequireAsync(ProjectItem item, Guid id, ProjectRole minimum, CancellationToken cancellationToken = default);

    // For code paths that are not controllers (controllers use the InstanceAdmin policy). Throws Forbidden unless the current user is an active instance admin.
    Task RequireInstanceAdminAsync(CancellationToken cancellationToken = default);
}
