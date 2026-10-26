using ShipLean.Contracts.Search;
using ShipLean.TestSupport.Builders;
using Npgsql;

namespace ShipLean.WebApi.Tests.Search.Index.Pass;

// ISearchDocWriter whose SyncAllAsync inserts one embedded-field row, as a real full sync would for a source that had none.
// A pass that embeds that row in the same call has run the sync first.
public sealed class InsertingSearchDocWriter(NpgsqlDataSource dataSource, Guid projectId, string text) : ISearchDocWriter
{
    public Guid InsertedId { get; private set; }

    public Task SyncAsync(IReadOnlyList<SearchSource> sources, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        InsertedId = await new SearchDocBuilder(projectId, SearchSourceKind.Sprint, Guid.NewGuid(), SearchField.SprintGoal, text).InsertAsync(connection);
    }
}
