using System.Data.Common;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Demos;

namespace ShipLean.WebApi.Tests.Demos;

// The real brand source, noting for each Inspect call whether a transaction of the host's was open (idle in transaction) at that moment.
internal sealed class BrandInspectionProbe(NpgsqlDataSource dataSource, IDemoBrandSource inner) : IDemoBrandSource
{
    private readonly List<bool> _transactionOpen = [];

    public IReadOnlyList<bool> TransactionOpen
    {
        get
        {
            lock (_transactionOpen)
            {
                return [.. _transactionOpen];
            }
        }
    }

    public async Task<DemoBrandInspection> InspectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var open = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = current_database() AND pid <> pg_backend_pid() AND state LIKE 'idle in transaction%')");
        lock (_transactionOpen)
        {
            _transactionOpen.Add(open);
        }

        return await inner.InspectAsync(projectId, cancellationToken);
    }

    public Task<DemoBrand> PrepareAsync(
        DemoBrandInspection inspection, Guid projectId, Guid itemId, Guid userId, Guid? authorizationId, string agentStamp, string baseUrl, DbTransaction transaction, CancellationToken cancellationToken = default)
        => inner.PrepareAsync(inspection, projectId, itemId, userId, authorizationId, agentStamp, baseUrl, transaction, cancellationToken);
}
