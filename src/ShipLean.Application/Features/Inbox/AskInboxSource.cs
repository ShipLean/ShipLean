using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Inbox;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core;

namespace ShipLean.Application.Features.Inbox;

// The open decisions and questions on items the user owns (AskOwnerSql.OwnedByUser). Only open ones count; confirmed, changed,
// answered and withdrawn asks are history. Ordering is the reader's.
public sealed class AskInboxSource(NpgsqlDataSource dataSource, TimeProvider clock) : IInboxAskSource
{
    public async Task<IReadOnlyList<InboxAskRow>> OpenAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var decisions = (await connection.QueryAsync<DecisionRow>(new CommandDefinition(
            $"""
            SELECT wi.id AS workItemId, d.number, d.question, d.recommended, d.alternatives::text AS alternatives,
                   d.created_by_user_id AS raisedBy, d.created_agent_name AS agentName, d.created_at AS at
            FROM decision d JOIN work_item wi ON wi.id = d.work_item_id
            WHERE d.state = 'open' AND wi.project_id = @projectId AND {AskOwnerSql.OwnedByUser}
            """,
            new { userId, projectId }, cancellationToken: cancellationToken))).ToList();
        var questions = (await connection.QueryAsync<QuestionRow>(new CommandDefinition(
            $"""
            SELECT wi.id AS workItemId, q.number, q.text, q.options::text AS options,
                   q.created_by_user_id AS askedBy, q.created_agent_name AS agentName, q.created_at AS at
            FROM question q JOIN work_item wi ON wi.id = q.work_item_id
            WHERE q.state = 'open' AND wi.project_id = @projectId AND {AskOwnerSql.OwnedByUser}
            """,
            new { userId, projectId }, cancellationToken: cancellationToken))).ToList();

        var userIds = decisions.Select(d => d.RaisedBy).Concat(questions.Select(q => q.AskedBy)).Distinct().ToArray();
        var users = (await connection.QueryAsync<UserRef>(new CommandDefinition(
            $"SELECT {UserRefs.Columns} FROM app_user u WHERE u.id = ANY(@userIds)",
            new { userIds }, cancellationToken: cancellationToken))).ToDictionary(u => u.Id);

        var rows = new List<InboxAskRow>();
        rows.AddRange(decisions.Select(d => new InboxAskRow(d.WorkItemId, new InboxAskDto(
            InboxAskKind.Decision, $"D{d.Number}", d.At, DisplayDate.Relative(d.At, clock), users[d.RaisedBy], d.AgentName,
            new InboxDecisionDto(d.Number, d.Question, d.Recommended, JsonSerializer.Deserialize<string[]>(d.Alternatives)!),
            null, null, null))));
        rows.AddRange(questions.Select(q => new InboxAskRow(q.WorkItemId, new InboxAskDto(
            InboxAskKind.Question, $"Q{q.Number}", q.At, DisplayDate.Relative(q.At, clock), users[q.AskedBy], q.AgentName,
            null, new InboxQuestionDto(q.Number, q.Text, JsonSerializer.Deserialize<string[]>(q.Options)!), null, null))));
        return rows;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> OpenCountsByProjectAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var counts = await connection.QueryAsync<(Guid ProjectId, int Count)>(new CommandDefinition(
            $"""
            SELECT wi.project_id, count(*)::int
            FROM (SELECT work_item_id FROM decision WHERE state = 'open'
                  UNION ALL SELECT work_item_id FROM question WHERE state = 'open') ask
            JOIN work_item wi ON wi.id = ask.work_item_id
            WHERE {AskOwnerSql.OwnedByUser}
            GROUP BY wi.project_id
            """,
            new { userId }, cancellationToken: cancellationToken));
        return counts.ToDictionary(c => c.ProjectId, c => c.Count);
    }

    public async Task<bool> EverAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            $"""
            SELECT EXISTS (SELECT 1 FROM work_item wi
                           WHERE {AskOwnerSql.OwnedByUser}
                             AND (EXISTS (SELECT 1 FROM decision d WHERE d.work_item_id = wi.id)
                                  OR EXISTS (SELECT 1 FROM question q WHERE q.work_item_id = wi.id)))
            """,
            new { userId }, cancellationToken: cancellationToken));
    }

    private sealed record DecisionRow(
        Guid WorkItemId, int Number, string Question, string Recommended, string Alternatives, Guid RaisedBy, string? AgentName,
        DateTimeOffset At);

    private sealed record QuestionRow(
        Guid WorkItemId, int Number, string Text, string Options, Guid AskedBy, string? AgentName, DateTimeOffset At);
}
