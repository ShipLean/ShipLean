using ShipLean.Contracts;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Identity;

// IProjectAccess that records each project a request passed the access check for, so a usage event can be tagged with it.
// A refused check throws before the stamp, so a project the caller cannot see is never stamped.
public sealed class UsageStampingProjectAccess(ProjectAccess inner, IUsageScope scope) : IProjectAccess
{
    public async Task<ProjectRole> RequireAsync(Guid projectId, ProjectRole minimum, CancellationToken cancellationToken = default)
    {
        var role = await inner.RequireAsync(projectId, minimum, cancellationToken);
        scope.ProjectTouched(projectId);
        return role;
    }

    public async Task<(Guid ProjectId, ProjectRole Role)> RequireAsync(ProjectItem item, Guid id, ProjectRole minimum, CancellationToken cancellationToken = default)
    {
        var found = await inner.RequireAsync(item, id, minimum, cancellationToken);
        scope.ProjectTouched(found.ProjectId);
        return found;
    }

    public Task RequireInstanceAdminAsync(CancellationToken cancellationToken = default)
        => inner.RequireInstanceAdminAsync(cancellationToken);
}
