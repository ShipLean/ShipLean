using ShipLean.Application.Features.Links.Rows;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.Links.Cards;

// ILinkCards over LinkCardRows. An address is read through the first ILinkCardSource (lowest Order) that handles it; when
// none does, the card is built from the address alone. A source's title is stored as the preview title, never as the typed
// title. agentStamp is not kept: work_item_link has no column for it and no card field reads it.
public sealed class LinkCardsService(LinkCardRows rows, LinkSourceReader reader, ICurrentUser currentUser, TimeProvider clock, IProjectEvents events) : ILinkCards
{
    private const int MaxUrlLength = 2048;

    private static readonly TimeSpan LiveStateTtl = TimeSpan.FromMinutes(10);

    public async Task<LinkCardsDto> ListAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        _ = await rows.ProjectOfAsync(workItemId, cancellationToken) ?? throw ApiException.NotFound("That work item does not exist.");
        var links = await rows.ListWorkItemLinksAsync(workItemId, cancellationToken);
        var cards = new List<LinkCardDto>();
        foreach (var row in await rows.ListAsync(workItemId, cancellationToken))
        {
            cards.Add(row.ToDto(clock, await IsRefreshDueAsync(row, cancellationToken)));
        }

        return new LinkCardsDto(links, [.. cards.Where(c => c.Group == LinkGroup.Code)], [.. cards.Where(c => c.Group == LinkGroup.Page)]);
    }

    public async Task<LinkPreviewDto> PreviewAsync(Guid workItemId, string url, CancellationToken cancellationToken = default)
    {
        _ = await rows.ProjectOfAsync(workItemId, cancellationToken) ?? throw ApiException.NotFound("That work item does not exist.");
        var uri = ParseAddress(url);
        url = url.Trim();
        var address = LinkAddress.Of(uri);
        var fetch = await reader.ReadAsync(uri, cancellationToken);
        if (fetch is null)
        {
            return new LinkPreviewDto(
                url, address.Kind, address.Group, url, LinkTitleSource.Url, address.Number, address.Location, address.Host, null,
                null, LinkStateSource.None, null, null, null, LinkPreviewStatus.None, MessageFor(address.Kind, LinkPreviewStatus.None));
        }

        var kind = LinkSourceReader.KindOf(fetch, address);
        var live = fetch.State is not null;
        var hasTitle = !string.IsNullOrWhiteSpace(fetch.Title);
        var titleSource = LinkTitleSource.Url;
        if (hasTitle)
        {
            titleSource = kind != LinkKind.Page && live ? LinkTitleSource.Live : LinkTitleSource.Fetched;
        }

        return new LinkPreviewDto(
            url, kind, kind == LinkKind.Page ? LinkGroup.Page : LinkGroup.Code,
            hasTitle ? fetch.Title! : url, titleSource,
            fetch.Number ?? address.Number, fetch.Location ?? address.Location, fetch.Site ?? address.Host, fetch.Author,
            fetch.State, live ? LinkStateSource.Live : LinkStateSource.None, null, null, fetch.Description, fetch.PreviewStatus, MessageFor(kind, fetch.PreviewStatus));
    }

    public async Task<LinkCardDto> AddUrlAsync(Guid workItemId, string url, string? title, string? note, string? agentStamp, bool readPreview = true, CancellationToken cancellationToken = default)
    {
        var projectId = await rows.ProjectOfAsync(workItemId, cancellationToken) ?? throw ApiException.NotFound("That work item does not exist.");
        var uri = ParseAddress(url);
        url = url.Trim();
        if ((await rows.ListAsync(workItemId, cancellationToken)).Any(r => r.Url == url))
        {
            throw ApiException.LinkDuplicate("That address is already linked.", "url");
        }

        var address = LinkAddress.Of(uri);
        var fetch = readPreview ? await reader.ReadAsync(uri, cancellationToken) : null;
        var now = clock.GetUtcNow();
        var id = await rows.InsertUrlAsync(workItemId, url, title, note, fetch is null ? address.Kind : LinkSourceReader.KindOf(fetch, address), currentUser.Id, now, cancellationToken);
        if (fetch is not null)
        {
            await rows.UpdatePreviewAsync(id, LinkSourceReader.PreviewOf(fetch, address, now), cancellationToken);
            if (fetch.State is { } state)
            {
                await rows.UpdateStateAsync(id, new LinkStateWrite(state, LinkStateSource.Live, now, null), cancellationToken);
            }
        }

        await events.PublishAsync(new LinksChanged(projectId, workItemId));
        return await CardAsync(id, cancellationToken);
    }

    public async Task<WorkItemLinkRowDto> AddWorkItemLinkAsync(Guid workItemId, string targetKey, LinkType type, CancellationToken cancellationToken = default)
    {
        var projectId = await rows.ProjectOfAsync(workItemId, cancellationToken) ?? throw ApiException.NotFound("That work item does not exist.");
        if (type == LinkType.Url)
        {
            throw ApiException.Validation("A link between work items needs a link type other than url.", "type");
        }

        var targetId = await rows.FindWorkItemByKeyAsync(projectId, targetKey, cancellationToken) ?? throw ApiException.NotFound("Linked work item not found.");
        if (targetId == workItemId)
        {
            throw ApiException.Validation("A work item can't link to itself.", "targetKey");
        }

        if ((await rows.ListWorkItemLinksAsync(workItemId, cancellationToken)).Any(l => l.WorkItemId == targetId && l.Type == type))
        {
            throw ApiException.LinkDuplicate("That work item is already linked with this link type.", "targetKey");
        }

        var id = await rows.InsertWorkItemLinkAsync(workItemId, targetId, type, currentUser.Id, clock.GetUtcNow(), cancellationToken);
        await events.PublishAsync(new LinksChanged(projectId, workItemId));
        return (await rows.FindWorkItemLinkAsync(id, cancellationToken))!;
    }

    public async Task<LinkCardDto> EditAsync(Guid linkId, string? title, string? note, CancellationToken cancellationToken = default)
    {
        var row = await rows.FindAsync(linkId, cancellationToken) ?? throw ApiException.NotFound("That link does not exist.");
        if (!await rows.UpdateTextAsync(linkId, title ?? row.Title, note ?? row.Note, cancellationToken))
        {
            throw ApiException.NotFound("That link does not exist.");
        }

        await events.PublishAsync(new LinksChanged(row.ProjectId, row.WorkItemId));
        return await CardAsync(linkId, cancellationToken);
    }

    public async Task ArchiveAsync(Guid linkId, CancellationToken cancellationToken = default)
    {
        var (workItemId, projectId) = await rows.ArchiveAsync(linkId, clock.GetUtcNow(), cancellationToken) ?? throw ApiException.NotFound("That link does not exist.");
        await events.PublishAsync(new LinksChanged(projectId, workItemId));
    }

    private async Task<LinkCardDto> CardAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await rows.FindAsync(id, cancellationToken) ?? throw ApiException.NotFound("That link does not exist.");
        return row.ToDto(clock, await IsRefreshDueAsync(row, cancellationToken));
    }

    private static Uri ParseAddress(string url)
    {
        url = url.Trim();
        return url.Length is > 0 and <= MaxUrlLength && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri
            : throw ApiException.LinkInvalidUrl($"Use a web address that starts with http:// or https:// and is at most {MaxUrlLength} characters.");
    }

    // Due: the last read found the host unreachable; a source can read it and none has yet; or its live state is older than ten minutes.
    private async Task<bool> IsRefreshDueAsync(LinkCardRow row, CancellationToken cancellationToken)
    {
        if (row.PreviewStatusValue == LinkPreviewStatus.Unreachable)
        {
            return true;
        }

        if (row.PreviewFetchedAt is null)
        {
            return Uri.TryCreate(row.Url, UriKind.Absolute, out var uri) && await reader.AnyHandlesAsync(uri, cancellationToken);
        }

        return row.OwnStateSource == "live" && row.StateAsOf is { } asOf && clock.GetUtcNow() - asOf > LiveStateTtl;
    }

    private static string MessageFor(LinkKind kind, LinkPreviewStatus status) => status switch
    {
        LinkPreviewStatus.Private => "Private address: no preview. An agent can report its state.",
        LinkPreviewStatus.Unreachable => "Couldn't reach that address: no preview yet. It will be tried again.",
        _ => kind switch
        {
            LinkKind.GithubPull => "GitHub pull request · state stays live",
            LinkKind.GithubIssue => "GitHub issue · state stays live",
            LinkKind.GitlabMergeRequest => "GitLab merge request · state stays live",
            LinkKind.GitlabIssue => "GitLab issue · state stays live",
            _ => "Web page · title and preview are read when it is added",
        },
    };
}
