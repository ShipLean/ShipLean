using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Search;

namespace ShipLean.WebApi.Tests.Search.Index.Pass;

// Base for the pass tests: a FakeEmbedder that counts its calls and a writer that records instead of writing. Rows are sprint
// goal and sprint name rows arranged with SearchDocBuilder (no item needed). Every test drains the rows earlier tests left, so
// its own counts start from zero.
public abstract class PassFixture : ApiFixture
{
    protected static readonly Guid Project = SeedIds.Projects.Ship;

    protected FakeEmbedder Embedder { get; } = new();

    protected RecordingSearchDocWriter Writer { get; } = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.Replace(ServiceDescriptor.Singleton<IEmbedder>(Embedder));
        services.Replace(ServiceDescriptor.Singleton<ISearchDocWriter>(Writer));
    };

    [SetUp]
    public async Task DrainLeftovers()
    {
        Embedder.ModelId = new EmbeddingModelId("fake-embedder", "1");
        await RunPassAsync(false);
    }

    protected async Task<IndexPassResult> RunPassAsync(bool full)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISearchIndexer>().RunPassAsync(full);
    }

    protected async Task<Guid> ArrangeAsync(string text, SearchField field = SearchField.SprintGoal, EmbeddingModelId? embeddedBy = null)
    {
        var builder = new SearchDocBuilder(Project, SearchSourceKind.Sprint, Guid.NewGuid(), field, text);
        if (embeddedBy is not null)
        {
            builder.WithEmbedding(new float[FakeEmbedder.VectorSize], embeddedBy);
        }
        await using var connection = await OpenDatabaseAsync();
        return await builder.InsertAsync(connection);
    }

    protected async Task<StoredEmbedding> StoredAsync(Guid id)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<StoredEmbedding>(
            "SELECT embedding IS NOT NULL AS HasEmbedding, embedding_model AS Model FROM search_doc WHERE id = @id", new { id });
    }

    protected sealed record StoredEmbedding(bool HasEmbedding, string? Model);

    protected static async Task<NpgsqlTransaction> LockAsync(NpgsqlConnection connection, Guid id)
    {
        var transaction = await connection.BeginTransactionAsync();
        await connection.ExecuteAsync("SELECT id FROM search_doc WHERE id = @id FOR UPDATE", new { id }, transaction);
        return transaction;
    }
}
