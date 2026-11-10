using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Inbox;

namespace ShipLean.Application.Features.Inbox;

// What the Inbox read routes do: the access check first (any member reads their own Inbox), then the read.
[RegisterPerRequest]
public sealed class InboxEndpoints(IProjectAccess access, IInboxReader inbox, IAnsweredReader answered)
{
    public async Task<InboxDto> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        return await inbox.GetAsync(projectId, cancellationToken);
    }

    public async Task<AnsweredDto> AnsweredAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        return await answered.GetAsync(projectId, cancellationToken);
    }
}
