using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.Links.Endpoints;

// What the link endpoints do: the access check first, then ILinkCards. Any member lists, adds, edits and archives links.
[RegisterPerRequest]
public sealed class LinkEndpoints(IProjectAccess access, ILinkCards cards, ILinkCardsRefresh refresh, ICurrentAgent agent)
{
    public async Task<LinkCardsDto> ListAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        return await cards.ListAsync(workItemId, cancellationToken);
    }

    public async Task<LinkPreviewDto> PreviewAsync(Guid workItemId, PreviewLinkRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        return await cards.PreviewAsync(workItemId, request.Url, cancellationToken);
    }

    public async Task<LinkCardDto> AddUrlAsync(Guid workItemId, AddLinkCardRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        return await cards.AddUrlAsync(workItemId, request.Url, request.Title, request.Note, AgentStamp.Of(agent), cancellationToken: cancellationToken);
    }

    public async Task<WorkItemLinkRowDto> AddWorkItemLinkAsync(Guid workItemId, AddWorkItemLinkRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        return await cards.AddWorkItemLinkAsync(workItemId, request.TargetKey, request.Type, cancellationToken);
    }

    public async Task<LinkCardDto> EditAsync(Guid linkId, EditLinkCardRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Link, linkId, ProjectRole.Member, cancellationToken);
        return await cards.EditAsync(linkId, request.Title, request.Note, cancellationToken);
    }

    public async Task ArchiveAsync(Guid linkId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Link, linkId, ProjectRole.Member, cancellationToken);
        await cards.ArchiveAsync(linkId, cancellationToken);
    }

    public async Task<LinkCardsDto> RefreshAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        return await refresh.RefreshAsync(workItemId, cancellationToken);
    }
}
