using Dapper;
using Npgsql;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Comments;

// Waits, by polling the server (each poll is a round trip, no sleep), until a backend is blocked on a row lock in a comment write.
internal static class LockWaiters
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    public static async Task WaitForOneAsync(NpgsqlConnection watcher)
    {
        using var timeout = new CancellationTokenSource(_timeout);
        while (!timeout.IsCancellationRequested)
        {
            var waiting = await watcher.ExecuteScalarAsync<int>(
                "SELECT count(*)::int FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query ILIKE '%UPDATE comment%'");
            if (waiting > 0)
            {
                return;
            }

            await Task.Yield();
        }

        throw new TimeoutException("No backend ever waited on the comment row lock.");
    }
}
