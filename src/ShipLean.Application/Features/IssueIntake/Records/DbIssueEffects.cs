using System.Data.Common;
using Dapper;
using Npgsql;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Records;

// github_issue_effect over Dapper. A claim is an insert: the primary key makes a second claim of the same key a no-op, and a
// concurrent claim waits on the first transaction's row lock until it commits (then loses) or rolls back (then wins).
public sealed class DbIssueEffects(NpgsqlDataSource dataSource, TimeProvider clock) : IIssueEffects
{
    public async Task<bool> TryClaimAsync(string key, Guid? issueId, Guid? result, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => await dataSource.RunAsync(
            (connection, tx) => connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO github_issue_effect (key, github_issue_id, result, created_at) VALUES (@key, @issueId, @result, @now)
                ON CONFLICT (key) DO NOTHING
                """,
                new { key, issueId, result, now = clock.GetUtcNow() },
                tx,
                cancellationToken: cancellationToken)),
            transaction,
            cancellationToken) == 1;

    public Task<Guid?> FindAsync(string key, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => dataSource.RunAsync(
            (connection, tx) => connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
                "SELECT result FROM github_issue_effect WHERE key = @key", new { key }, tx, cancellationToken: cancellationToken)),
            transaction,
            cancellationToken);
}
