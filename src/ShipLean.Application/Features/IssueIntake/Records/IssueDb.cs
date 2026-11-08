using System.Data.Common;
using Npgsql;

namespace ShipLean.Application.Features.IssueIntake.Records;

// Runs a query on the caller's transaction when there is one, or on a connection of its own.
internal static class IssueDb
{
    public static async Task<T> RunAsync<T>(
        this NpgsqlDataSource dataSource, Func<DbConnection, DbTransaction?, Task<T>> work, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            return await work(transaction.Connection!, transaction);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await work(connection, null);
    }
}
