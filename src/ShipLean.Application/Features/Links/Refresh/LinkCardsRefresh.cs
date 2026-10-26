using ShipLean.Application.Features.Links.Cards;
using ShipLean.Application.Features.Links.Rows;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.Refresh;

// Reads again the item's cards that are due, at most four at a time and five seconds each, through the same sources the card service uses.
// A read that found the address unreachable changes nothing, and a read with no state leaves the card's state alone, so a failed
// fetch never overwrites what an agent reported. A read with a state replaces the card's own state (source Live).
public sealed class LinkCardsRefresh(ILinkCards cards, LinkCardRows rows, LinkSourceReader reader, TimeProvider clock, IProjectEvents events) : ILinkCardsRefresh
{
    private const int MaxParallel = 4;

    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(5);

    public async Task<LinkCardsDto> RefreshAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        var listed = await cards.ListAsync(workItemId, cancellationToken);
        var due = listed.Code.Concat(listed.Pages).Where(c => c.RefreshDue).Select(c => c.Id).ToList();
        if (due.Count == 0)
        {
            return listed;
        }

        var updated = 0;
        await Parallel.ForEachAsync(due, new ParallelOptions { MaxDegreeOfParallelism = MaxParallel, CancellationToken = cancellationToken }, async (id, token) =>
        {
            if (await RefreshOneAsync(id, token))
            {
                Interlocked.Increment(ref updated);
            }
        });

        if (updated > 0 && await rows.ProjectOfAsync(workItemId, cancellationToken) is { } projectId)
        {
            await events.PublishAsync(new LinksChanged(projectId, workItemId));
        }

        return await cards.ListAsync(workItemId, cancellationToken);
    }

    private async Task<bool> RefreshOneAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await rows.FindAsync(id, cancellationToken);
        if (row is null || !Uri.TryCreate(row.Url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var fetch = await ReadWithinTimeoutAsync(uri, cancellationToken);
        if (fetch is null || fetch.PreviewStatus == LinkPreviewStatus.Unreachable)
        {
            return false;
        }

        var now = clock.GetUtcNow();
        var preview = LinkSourceReader.PreviewOf(fetch, LinkAddress.Of(uri), now);
        await rows.UpdatePreviewAsync(id, string.IsNullOrWhiteSpace(preview.Title) ? preview with { Title = row.PreviewTitle } : preview, cancellationToken);
        if (fetch.State is { } state)
        {
            await rows.UpdateStateAsync(id, new LinkStateWrite(state, LinkStateSource.Live, now, null), cancellationToken);
        }

        return true;
    }

    // Null when no source handles the address or the read took longer than the timeout.
    private async Task<LinkCardFetch?> ReadWithinTimeoutAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(FetchTimeout);
        try
        {
            return await reader.ReadAsync(uri, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
