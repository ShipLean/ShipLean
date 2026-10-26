using System.Data.Common;
using ShipLean.Contracts.Projects;

namespace ShipLean.WebApi.Tests.States.Store;

// Commits the transaction a write was given when it succeeds, and rolls it back when it throws: the caller's part of the contract.
internal sealed class CommittingStateCatalog(IStateCatalog inner) : IStateCatalog
{
    public Task<IReadOnlyList<StateDto>> ListAsync(Guid projectId, CancellationToken cancellationToken = default) => inner.ListAsync(projectId, cancellationToken);

    public Task CreateDefaultsAsync(Guid projectId, DbTransaction transaction, CancellationToken cancellationToken = default)
        => FinishAsync(transaction, () => inner.CreateDefaultsAsync(projectId, transaction, cancellationToken));

    public async Task<IReadOnlyDictionary<Guid, Guid>> CopyAsync(Guid fromProjectId, Guid toProjectId, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<Guid, Guid> map = null!;
        await FinishAsync(transaction, async () => map = await inner.CopyAsync(fromProjectId, toProjectId, transaction, cancellationToken));
        return map;
    }

    public Task ApplyAsync(Guid projectId, StateChanges changes, DbTransaction transaction, CancellationToken cancellationToken = default)
        => FinishAsync(transaction, () => inner.ApplyAsync(projectId, changes, transaction, cancellationToken));

    private static async Task FinishAsync(DbTransaction transaction, Func<Task> write)
    {
        var connection = transaction.Connection!;
        await using (connection)
        await using (transaction)
        {
            try
            {
                await write();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            await transaction.CommitAsync();
        }
    }
}
