using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Inbox;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core;

namespace ShipLean.Application.Features.Inbox;

// The status lines on the user's Features that need checking: a Feature the user owns (AskOwnerSql.OwnedByUser), not Completed,
// with a non-empty line that was never verified or was last verified more than InboxRules.StatusStaleAfter ago. A Feature
// with no status line asks nothing. Ordering is the reader's.
public sealed class StatusLineInboxSource(NpgsqlDataSource dataSource, TimeProvider clock) : IInboxAskSource
{
    private const string StaleFeatures =
        $"""
        wi.type = 'feature' AND wi.status_line IS NOT NULL AND btrim(wi.status_line) <> ''
        AND (wi.status_verified_at IS NULL OR wi.status_verified_at < @staleBefore)
        AND {AskOwnerSql.OwnedByUser}
        AND EXISTS (SELECT 1 FROM state feature_state WHERE feature_state.id = wi.state_id AND feature_state.category <> 'completed')
        """;

    public async Task<IReadOnlyList<InboxAskRow>> OpenAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var features = (await connection.QueryAsync<Row>(new CommandDefinition(
            $"""
            SELECT wi.id, wi.status_line AS statusLine, wi.status_verified_at AS verifiedAt, wi.created_at AS createdAt,
                   wi.status_verified_by AS verifiedBy, wi.status_verified_agent AS agentName
            FROM work_item wi
            WHERE wi.project_id = @projectId AND {StaleFeatures}
            """,
            new { userId, projectId, staleBefore = StaleBefore() }, cancellationToken: cancellationToken))).ToList();
        var userIds = features.Select(f => f.VerifiedBy).OfType<Guid>().Distinct().ToArray();
        var users = (await connection.QueryAsync<UserRef>(new CommandDefinition(
            $"SELECT {UserRefs.Columns} FROM app_user u WHERE u.id = ANY(@userIds)",
            new { userIds }, cancellationToken: cancellationToken))).ToDictionary(u => u.Id);

        return [.. features.Select(f =>
        {
            var at = f.VerifiedAt ?? f.CreatedAt;
            return new InboxAskRow(f.Id, new InboxAskDto(
                InboxAskKind.Status, "Status", at, DisplayDate.Relative(at, clock),
                f.VerifiedBy is { } by ? users[by] : null, f.AgentName, null, null, null,
                new InboxStatusDto(f.StatusLine, f.VerifiedAt)));
        })];
    }

    public async Task<IReadOnlyDictionary<Guid, int>> OpenCountsByProjectAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var counts = await connection.QueryAsync<(Guid ProjectId, int Count)>(new CommandDefinition(
            $"SELECT wi.project_id, count(*)::int FROM work_item wi WHERE {StaleFeatures} GROUP BY wi.project_id",
            new { userId, staleBefore = StaleBefore() }, cancellationToken: cancellationToken));
        return counts.ToDictionary(c => c.ProjectId, c => c.Count);
    }

    // Whether the user owns a Feature with a status line, however recently it was checked.
    public async Task<bool> EverAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            $"""
            SELECT EXISTS (SELECT 1 FROM work_item wi
                           WHERE wi.type = 'feature' AND wi.status_line IS NOT NULL AND btrim(wi.status_line) <> ''
                             AND {AskOwnerSql.OwnedByUser})
            """,
            new { userId }, cancellationToken: cancellationToken));
    }

    private DateTimeOffset StaleBefore() => clock.GetUtcNow() - InboxRules.StatusStaleAfter;

    private sealed record Row(Guid Id, string StatusLine, DateTimeOffset? VerifiedAt, DateTimeOffset CreatedAt, Guid? VerifiedBy, string? AgentName);
}
