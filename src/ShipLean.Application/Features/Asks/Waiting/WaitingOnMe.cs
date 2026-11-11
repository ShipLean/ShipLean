using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Asks;
using ShipLean.Core;

namespace ShipLean.Application.Features.Asks.Waiting;

// The open asks on the items the signed-in person owns, across their projects. The owner is coalesce(assigned_to, created_by),
// never "assigned to or created by". Neither work items nor projects are archived; a removed item and a project the person has
// left (project_member.archived_at) drop out. Items are ordered by their oldest open ask.
public sealed class WaitingOnMe(NpgsqlDataSource dataSource, ICurrentUser currentUser, TimeProvider clock) : IWaitingOnMe
{
    public async Task<WaitingDto> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<Row>(new CommandDefinition(
            """
            SELECT p.id AS projectId, p.key AS projectKey, p.name AS projectName, w.id, w.key, w.type, w.title,
                   a.openQuestions, a.openDecisions, a.oldestAt
            FROM work_item w
            JOIN project p ON p.id = w.project_id
            JOIN state s ON s.id = w.state_id
            JOIN project_member m ON m.project_id = w.project_id AND m.user_id = @me AND m.archived_at IS NULL
            CROSS JOIN LATERAL (
                SELECT (SELECT count(*) FROM question q WHERE q.work_item_id = w.id AND q.state = 'open')::int AS openQuestions,
                       (SELECT count(*) FROM decision d WHERE d.work_item_id = w.id AND d.state = 'open')::int AS openDecisions,
                       least((SELECT min(q.created_at) FROM question q WHERE q.work_item_id = w.id AND q.state = 'open'),
                             (SELECT min(d.created_at) FROM decision d WHERE d.work_item_id = w.id AND d.state = 'open')) AS oldestAt
            ) a
            WHERE coalesce(w.assigned_to, w.created_by) = @me AND s.category <> 'removed' AND a.oldestAt IS NOT NULL
            ORDER BY a.oldestAt, w.key
            """,
            new { me = currentUser.Id }, cancellationToken: cancellationToken))).ToList();
        var items = rows.Select(r => new WaitingItemDto(
            r.ProjectId, r.ProjectKey, r.ProjectName, r.Id, r.Key, r.Type, r.Title, r.OpenQuestions, r.OpenDecisions, r.OldestAt,
            DisplayDate.Relative(r.OldestAt, clock))).ToList();
        return new WaitingDto(items.Sum(i => i.OpenQuestions), items.Sum(i => i.OpenDecisions), items);
    }

    private sealed record Row(
        Guid ProjectId, string ProjectKey, string ProjectName, Guid Id, string Key, string Type, string Title, int OpenQuestions,
        int OpenDecisions, DateTimeOffset OldestAt);
}
