using ShipLean.Application.Features.Links.Rows;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.Reports;

// ILinkStateReports over LinkCardRows. A reported state is the card's own (source Reported, as of the agent's time, stamped with
// the agent); a reported title is kept as the preview title, so a title a person typed still wins. Every matching link in a project
// the user is a member of is written, and each item it sits on gets linksChanged.
public sealed class LinkStateReports(LinkCardRows rows, ICurrentUser currentUser, ICurrentAgent agent, IProjectEvents events) : ILinkStateReports
{
    public async Task ReportAsync(string url, string? title, LinkState? state, DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        var matches = await rows.FindByUrlAsync(url.Trim(), currentUser.Id, cancellationToken);
        if (matches.Count == 0)
        {
            throw ApiException.NotFound("No link with that address is on a work item you can reach.");
        }

        var stamp = AgentStamp.Of(agent);
        foreach (var row in matches)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                await rows.UpdatePreviewAsync(row.Id, new LinkPreviewWrite(
                    title.Trim(), row.PreviewDescription, row.PreviewSite, row.PreviewImageKey, row.PreviewFaviconKey, row.PreviewStatusValue,
                    row.PreviewFetchedAt ?? asOf, row.Number, row.Location, row.Author), cancellationToken);
            }

            if (state is { } reported)
            {
                await rows.UpdateStateAsync(row.Id, new LinkStateWrite(reported, LinkStateSource.Reported, asOf, stamp), cancellationToken);
            }
        }

        foreach (var item in matches.Select(r => (r.ProjectId, r.WorkItemId)).Distinct())
        {
            await events.PublishAsync(new LinksChanged(item.ProjectId, item.WorkItemId));
        }
    }
}
