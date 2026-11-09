using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.Refs;
using ShipLean.Core;

namespace ShipLean.Application.Features.Asks.History;

// A work item's ask lines from ask_event, newest first. Decisions raised (or questions asked) by the same person and agent at
// the same moment fold into one line when their numbers run on: "D1–D5". Trusts its caller to have checked access.
public sealed class AskHistory(NpgsqlDataSource dataSource, TimeProvider clock) : IAskHistory
{
    public async Task<IReadOnlyList<AskHistoryLineDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var events = (await connection.QueryAsync<EventRow>(new CommandDefinition(
            """
            SELECT e.kind, d.number AS decisionNumber, q.number AS questionNumber, e.from_answer AS fromAnswer, e.to_answer AS toAnswer,
                   e.note, e.by_user_id AS byUser, e.agent_name AS agentName, e.at
            FROM ask_event e
            LEFT JOIN decision d ON d.id = e.decision_id
            LEFT JOIN question q ON q.id = e.question_id
            WHERE e.work_item_id = @workItemId
            """,
            new { workItemId }, cancellationToken: cancellationToken))).ToList();
        var users = (await connection.QueryAsync<UserRef>(new CommandDefinition(
            $"SELECT {UserRefs.Columns} FROM app_user u WHERE u.id = ANY(@ids)",
            new { ids = events.Select(e => e.ByUser).OfType<Guid>().Distinct().ToArray() }, cancellationToken: cancellationToken)))
            .ToDictionary(u => u.Id);

        var lines = new List<(int Number, AskHistoryLineDto Line)>();
        foreach (var e in events.Where(e => !IsRaise(e.Kind)))
        {
            lines.Add((e.Number, Line(e, Badge(e), users)));
        }

        var folds = events.Where(e => IsRaise(e.Kind)).GroupBy(e => (e.Kind, e.ByUser, e.AgentName, e.At));
        foreach (var fold in folds)
        {
            foreach (var run in Runs([.. fold.OrderBy(e => e.Number)]))
            {
                var first = run[0];
                lines.Add(run.Count == 1
                    ? (first.Number, Line(first, Badge(first), users))
                    : (first.Number, Line(first, $"{Prefix(first)}{first.Number}–{Prefix(first)}{run[^1].Number}", users) with { To = null }));
            }
        }

        return [.. lines.OrderByDescending(l => l.Line.At).ThenByDescending(l => l.Number).Select(l => l.Line)];
    }

    private AskHistoryLineDto Line(EventRow e, string badge, Dictionary<Guid, UserRef> users)
    {
        var kind = Enum.Parse<AskEventKind>(e.Kind, true);
        return new AskHistoryLineDto(
            kind, badge, e.FromAnswer, kind == AskEventKind.Withdrawn ? null : e.ToAnswer, e.Note,
            e.ByUser is { } by ? users.GetValueOrDefault(by) : null, e.AgentName, e.At, DisplayDate.Relative(e.At, clock));
    }

    private static List<List<EventRow>> Runs(List<EventRow> sorted)
    {
        var runs = new List<List<EventRow>>();
        foreach (var e in sorted)
        {
            if (runs.Count > 0 && runs[^1][^1].Number + 1 == e.Number)
            {
                runs[^1].Add(e);
            }
            else
            {
                runs.Add([e]);
            }
        }

        return runs;
    }

    private static bool IsRaise(string kind) => kind is "raised" or "asked";

    private static string Prefix(EventRow e) => e.DecisionNumber is null ? "Q" : "D";

    private static string Badge(EventRow e) => $"{Prefix(e)}{e.Number}";

    private sealed record EventRow(
        string Kind, int? DecisionNumber, int? QuestionNumber, string? FromAnswer, string? ToAnswer, string? Note, Guid? ByUser,
        string? AgentName, DateTimeOffset At)
    {
        public int Number => DecisionNumber ?? QuestionNumber!.Value;
    }
}
