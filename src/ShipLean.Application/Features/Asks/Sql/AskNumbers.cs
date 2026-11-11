using System.Data.Common;
using Dapper;

namespace ShipLean.Application.Features.Asks.Sql;

// Hands out decision and question numbers on one work item. Take LockAsync first, in the writing transaction: it serialises
// every writer of that item's asks until commit, so two writers never compute the same next number. Numbers are max + 1,
// counted separately for decisions (D1, D2…) and questions (Q1, Q2…); withdrawn rows still count, so a number is never
// reused. The unique (work_item_id, number) constraints are the backstop. Public because WebApi.Tests call it.
public static class AskNumbers
{
    public static Task LockAsync(DbTransaction transaction, Guid workItemId, CancellationToken cancellationToken = default)
        => ConnectionOf(transaction).ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtextextended('ask_numbers:' || @workItemId::text, 0))",
            new { workItemId }, transaction, cancellationToken: cancellationToken));

    public static Task<int> NextDecisionAsync(DbTransaction transaction, Guid workItemId, CancellationToken cancellationToken = default)
        => ConnectionOf(transaction).ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT coalesce(max(number), 0)::int + 1 FROM decision WHERE work_item_id = @workItemId",
            new { workItemId }, transaction, cancellationToken: cancellationToken));

    public static Task<int> NextQuestionAsync(DbTransaction transaction, Guid workItemId, CancellationToken cancellationToken = default)
        => ConnectionOf(transaction).ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT coalesce(max(number), 0)::int + 1 FROM question WHERE work_item_id = @workItemId",
            new { workItemId }, transaction, cancellationToken: cancellationToken));

    private static DbConnection ConnectionOf(DbTransaction transaction)
        => transaction.Connection ?? throw new InvalidOperationException("The transaction is no longer open.");
}
