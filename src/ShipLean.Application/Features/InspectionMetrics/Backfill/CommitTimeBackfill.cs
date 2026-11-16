using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Application.Plans;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.InspectionMetrics;
using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.InspectionMetrics.Backfill;

// TODO: temporary; remove once existing installs are backfilled
public sealed class CommitTimeBackfill(NpgsqlDataSource dataSource, ICurrentUser currentUser, UserSessions sessions, ILinkCards linkCards) : ICommitTimeBackfill
{
    private const int MaxPage = 500;

    private sealed record LinkRow(Guid Id, Guid SourceId, string Url, string Title, string ItemKey, bool HasTime);

    public async Task<CommitTimeMissingPageDto> ListMissingAsync(int limit, string? after, CancellationToken cancellationToken = default)
    {
        await RequireInstanceAdminAsync(cancellationToken);
        if (limit is < 1 or > MaxPage)
        {
            throw ApiException.Validation($"limit must be between 1 and {MaxPage}.", "limit");
        }
        if (after is not null && !Guid.TryParse(after, out _))
        {
            throw ApiException.Validation("after must be the next_cursor of the previous page.", "after");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<LinkRow>(new CommandDefinition(
            """
            SELECT l.id AS Id, l.source_id AS SourceId, l.url AS Url, l.title AS Title, w.key AS ItemKey, false AS HasTime
            FROM work_item_link l JOIN work_item w ON w.id = l.source_id
            WHERE l.link_type = 'url' AND l.archived_at IS NULL AND l.committed_at IS NULL AND starts_with(l.title, @prefix)
              AND (@after::uuid IS NULL OR l.id > @after::uuid)
            ORDER BY l.id
            LIMIT @take
            """,
            new { prefix = PlanMarkers.CommitPrefix, after = after is null ? (Guid?)null : Guid.Parse(after), take = limit + 1 },
            cancellationToken: cancellationToken))).ToList();

        var page = rows.Take(limit).ToList();
        var links = page.Select(MissingOf).OfType<MissingCommitTimeDto>().ToList();
        return new CommitTimeMissingPageDto(links, rows.Count > limit ? page[^1].Id.ToString() : null);
    }

    public async Task<CommitTimeBackfillCountsDto> SetAsync(IReadOnlyList<CommitTimeDto> commits, CancellationToken cancellationToken = default)
    {
        await RequireInstanceAdminAsync(cancellationToken);
        if (commits.Count is < 1 or > MaxPage)
        {
            throw ApiException.Validation($"Send between 1 and {MaxPage} commits.", "commits");
        }

        var updated = 0;
        var alreadySet = 0;
        var notFound = 0;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        foreach (var commit in commits.DistinctBy(c => c.Sha[..7].ToLowerInvariant()))
        {
            var sha7 = commit.Sha[..7].ToLowerInvariant();
            var links = (await connection.QueryAsync<LinkRow>(new CommandDefinition(
                """
                SELECT l.id AS Id, l.source_id AS SourceId, l.url AS Url, l.title AS Title, w.key AS ItemKey, l.committed_at IS NOT NULL AS HasTime
                FROM work_item_link l JOIN work_item w ON w.id = l.source_id
                WHERE l.link_type = 'url' AND l.archived_at IS NULL AND (l.title = @exact OR starts_with(l.title, @withSpace))
                """,
                new { exact = PlanMarkers.CommitPrefix + sha7, withSpace = PlanMarkers.CommitPrefix + sha7 + " " },
                cancellationToken: cancellationToken))).ToList();
            if (links.Count == 0)
            {
                notFound++;
            }
            foreach (var link in links)
            {
                // The one guarded writer: it sets the time only while the link has none, so a race loses to the earlier writer.
                if (!link.HasTime && await linkCards.SetCommitTimeAsync(link.SourceId, link.Url, commit.CommittedAt, cancellationToken))
                {
                    updated++;
                }
                else
                {
                    alreadySet++;
                }
            }
        }
        return new CommitTimeBackfillCountsDto(updated, alreadySet, notFound);
    }

    private static MissingCommitTimeDto? MissingOf(LinkRow row)
        => PlanMarkers.TryReadCommit(row.Title, out var sha, out _) ? new MissingCommitTimeDto(sha, row.Url, row.ItemKey) : null;

    // Anyone else, a disabled admin included, gets the same answer as for a tool that does not exist.
    private async Task RequireInstanceAdminAsync(CancellationToken cancellationToken)
    {
        if (await sessions.GetFreshAsync(currentUser.Id, cancellationToken) is not { IsAdmin: true, Disabled: false })
        {
            throw ApiException.NotFound("That was not found.");
        }
    }
}
