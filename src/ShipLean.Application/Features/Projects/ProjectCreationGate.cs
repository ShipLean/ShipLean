using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Projects;

namespace ShipLean.Application.Features.Projects;

// What must hold before a copy: the caller is a member of the project they copy from (anyone else's is NotFound). The
// project_creation rule lives in the factory itself.
[RegisterPerRequest]
public sealed class ProjectCreationGate(IProjectAccess access)
{
    public async Task RequireAsync(ProjectCreate create, CancellationToken cancellationToken = default)
    {
        if (create?.CopyFromProjectId is { } source)
        {
            await access.RequireAsync(source, ProjectRole.Member, cancellationToken);
        }
    }
}
