using System.Data.Common;
using Dapper;

namespace ShipLean.Application.Features.Asks.Sql;

// Marks a comment the asks section posted, in the caller's transaction: kind answer (with a question), changed or about (with a
// decision). The table checks that exactly one of decisionId and questionId is set. Public because WebApi.Tests call it.
public static class AskTagWriter
{
    public static Task InsertAsync(
        DbTransaction transaction, Guid commentId, string kind, Guid? decisionId, Guid? questionId, CancellationToken cancellationToken = default)
        => ConnectionOf(transaction).ExecuteAsync(new CommandDefinition(
            "INSERT INTO comment_ask_tag (comment_id, kind, decision_id, question_id) VALUES (@commentId, @kind, @decisionId, @questionId)",
            new { commentId, kind, decisionId, questionId }, transaction, cancellationToken: cancellationToken));

    private static DbConnection ConnectionOf(DbTransaction transaction)
        => transaction.Connection ?? throw new InvalidOperationException("The transaction is no longer open.");
}
