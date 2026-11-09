using System.Data.Common;
using Dapper;

namespace ShipLean.Application.Features.Asks.Sql;

// Appends one row to ask_event, in the caller's transaction. Events are never changed or removed. Public because WebApi.Tests call it.
public static class AskEventWriter
{
    public static Task InsertAsync(DbTransaction transaction, AskEventRow row, CancellationToken cancellationToken = default)
        => ConnectionOf(transaction).ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO ask_event (id, work_item_id, decision_id, question_id, kind, from_answer, to_answer, to_kind, note,
                                   by_user_id, agent_name, at, from_description)
            VALUES (@Id, @WorkItemId, @DecisionId, @QuestionId, @Kind, @FromAnswer, @ToAnswer, @ToKind, @Note,
                    @ByUserId, @AgentName, @At, @FromDescription)
            """,
            row with { At = row.At.ToUniversalTime() }, transaction, cancellationToken: cancellationToken));

    private static DbConnection ConnectionOf(DbTransaction transaction)
        => transaction.Connection ?? throw new InvalidOperationException("The transaction is no longer open.");
}
