using System.Data;
using Dapper;
using Npgsql;

namespace ShipLean.Application.Features.Backups.Runs;

// The lease's own unpooled connection, one command at a time (the loss check and ClaimPruneAsync share it). A command that
// fails and leaves the connection broken or closed means the lock went with it: Lost is cancelled and
// BackupLeaseLostException is thrown. Other database errors pass through.
internal sealed class BackupLeaseConnection(NpgsqlConnection connection) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lost = new();

    public CancellationToken Lost => _lost.Token;

    public async Task<T> UseAsync<T>(Func<NpgsqlConnection, Task<T>> command, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_lost.IsCancellationRequested)
            {
                throw new BackupLeaseLostException("The backup lease was lost.");
            }
            return await command(connection);
        }
        catch (NpgsqlException exception) when (!IsOpen)
        {
            await SignalLostAsync();
            throw new BackupLeaseLostException("The backup lease was lost: its connection closed.", exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Throws BackupLeaseLostException unless this connection still holds the lock.
    public async Task EnsureHeldAsync(CancellationToken cancellationToken)
    {
        var held = await UseAsync(
            c => c.ExecuteScalarAsync<bool>(new CommandDefinition(BackupLeaseLock.HeldSql, BackupLeaseLock.Parameters, cancellationToken: cancellationToken)),
            cancellationToken);
        if (!held)
        {
            await SignalLostAsync();
            throw new BackupLeaseLostException("The backup lease was lost: its connection no longer holds the lock.");
        }
    }

    public Task SignalLostAsync() => _lost.IsCancellationRequested ? Task.CompletedTask : _lost.CancelAsync();

    // Unlocks first, so a start right after this sees the lock free: closing alone releases it only once the server
    // notices the session ended. The connection is not pooled, so closing it always ends the session.
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            if (IsOpen)
            {
                await connection.ExecuteAsync(new CommandDefinition(BackupLeaseLock.UnlockSql, BackupLeaseLock.Parameters, cancellationToken: CancellationToken.None));
            }
        }
        catch (NpgsqlException)
        {
            // Closing below ends the session, and the lock with it.
        }
        finally
        {
            await connection.DisposeAsync();
            _gate.Release();
        }
        _gate.Dispose();
        _lost.Dispose();
    }

    private bool IsOpen => connection.FullState == ConnectionState.Open;
}
