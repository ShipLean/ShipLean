using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Application.WorkItems.History;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Screens;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems;
using ShipLean.Contracts.WorkItems.Reads.Changes;
using ShipLean.Contracts.WorkItems.Reads.Tree;
using ShipLean.Core;

namespace ShipLean.Application.Features.FeaturesOverview;

// The Features overview over the tree reads: one stack read of the project's Features (rank is the place in it, as the Features
// backlog ranks), one roll-up read, one Needs you read and one query for the status lines, verified stamps and health the stack
// rows do not carry. Callers check Member access first.
public sealed class FeaturesOverview(
    NpgsqlDataSource dataSource,
    IWorkTreeReads reads,
    IWorkItemChanges changes,
    INeedsYouCounts needsYou,
    ICurrentUser currentUser,
    TimeProvider clock) : IFeaturesOverview
{
    private const int ChangesCap = 500;

    public async Task<FeaturesOverviewDto> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var seenAt = await connection.ExecuteScalarAsync<DateTimeOffset?>(new CommandDefinition(
            "SELECT seen_at FROM feature_digest_seen WHERE user_id = @userId AND project_id = @projectId",
            new { userId = currentUser.Id, projectId }, cancellationToken: cancellationToken));
        var (since, sinceLabel) = FeaturesSince.Of(seenAt, clock);
        var changed = (await changes.SinceTimeAsync(projectId, since, ChangesCap, null, cancellationToken)).Entries.Count;

        // Features waiting in an intake (Triage) state are kept out of the backlog, so they are out of the rank too.
        var intake = (await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT id FROM state WHERE project_id = @projectId AND is_intake AND type = 'feature'",
            new { projectId }, cancellationToken: cancellationToken))).ToHashSet();
        var stack = (await reads.StackAsync(projectId, StackLevel.Feature, null, true, cancellationToken))
            .Where(row => !intake.Contains(row.State.Id))
            .ToList();
        if (stack.Count == 0)
        {
            return new FeaturesOverviewDto(projectId, sinceLabel, changed, []);
        }

        var ids = stack.Select(row => row.Id).ToList();
        var fields = await FieldsAsync(connection, ids, cancellationToken);
        var rollups = (await reads.RollupsAsync(projectId, ids, null, cancellationToken)).ToLookup(r => r.ParentId);
        var waiting = await needsYou.ForItemsAsync(currentUser.Id, ids, cancellationToken);
        var now = clock.GetUtcNow();

        var rows = stack.Select((item, index) =>
        {
            var field = fields[item.Id];
            var line = string.IsNullOrEmpty(field.StatusLine) ? null : field.StatusLine;
            return new FeatureRowDto(
                item.Id,
                item.Key,
                item.Title,
                item.Revision,
                item.State,
                WorkItemFieldView.ParseHealth(field.Health),
                line,
                field.Verified,
                line is not null && (field.Verified is null || now - field.Verified.At > FeatureRules.StatusStaleAfter),
                [.. rollups[item.Id].Where(r => r.Type != WorkItemTypeId.Task).Select(RollupCountDto.From)],
                waiting.GetValueOrDefault(item.Id),
                index + 1,
                item.Parent is { Type.Id: WorkItemTypeId.Epic } ? item.Parent : null);
        });
        return new FeaturesOverviewDto(projectId, sinceLabel, changed, FeatureGroups.Group(rows));
    }

    private async Task<IReadOnlyDictionary<Guid, Fields>> FieldsAsync(NpgsqlConnection connection, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var rows = (await connection.QueryAsync<FieldRow>(new CommandDefinition(
            """
            SELECT id, COALESCE(status_line, '') AS statusLine, status_verified_at AS verifiedAt, status_verified_by AS verifiedBy,
                   status_verified_agent AS verifiedAgent, status_verified_via AS verifiedVia, health
            FROM work_item WHERE id = ANY(@ids)
            """,
            new { ids = ids.ToArray() }, cancellationToken: cancellationToken))).ToList();
        var users = (await connection.QueryAsync<UserRef>(new CommandDefinition(
            $"SELECT {UserRefs.Columns} FROM app_user u WHERE u.id = ANY(@ids)",
            new { ids = rows.Select(r => r.VerifiedBy).OfType<Guid>().Distinct().ToArray() }, cancellationToken: cancellationToken))).ToDictionary(u => u.Id);
        return rows.ToDictionary(
            r => r.Id,
            r => new Fields(
                r.StatusLine,
                r.Health,
                Stamp(r, users)));
    }

    private StatusVerifiedDto? Stamp(FieldRow r, IReadOnlyDictionary<Guid, UserRef> users)
    {
        if (r.VerifiedAt is not { } at)
        {
            return null;
        }

        var by = r.VerifiedBy is { } id ? users.GetValueOrDefault(id) : null;
        return new StatusVerifiedDto(at, DisplayDate.Relative(at, clock), StatusVerifiedDto.IsStale(at, clock.GetUtcNow()), by, r.VerifiedAgent, r.VerifiedVia);
    }

    private sealed record FieldRow(Guid Id, string StatusLine, DateTimeOffset? VerifiedAt, Guid? VerifiedBy, string? VerifiedAgent, string? VerifiedVia, string? Health);

    private sealed record Fields(string StatusLine, string? Health, StatusVerifiedDto? Verified);
}
