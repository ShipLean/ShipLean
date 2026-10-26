using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Features.Search.Ranking;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Search.Semantic;

// Shared arrangement for the semantic leg's tests: the host's FakeEmbedder, rows embedded with it, and the leg called directly.
public abstract class SemanticFixture : ApiFixture
{
    // The class shares one database, so each test starts with an empty index.
    [SetUp]
    public async Task EmptyIndexAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("DELETE FROM search_doc");
    }

    protected IEmbedder Embedder => Host.Services.GetRequiredService<IEmbedder>();

    protected static Task<Guid> ItemAsync(NpgsqlConnection connection, string title, Guid? projectId = null, Guid? stateId = null, string? type = null, string[]? tags = null)
    {
        var builder = new WorkItemBuilder(projectId ?? SeedIds.Projects.Ship).WithTitle(title);
        if (type != null)
        {
            builder.WithType(type);
        }

        if (tags != null)
        {
            builder.WithTags(tags);
        }

        if (stateId != null)
        {
            builder.WithState(stateId.Value);
        }

        return builder.InsertAsync(connection);
    }

    // A row of the item's, embedded with the host's embedder unless embedded is false.
    protected async Task<Guid> RowAsync(
        NpgsqlConnection connection,
        Guid itemId,
        SearchField field,
        string text,
        bool embedded = true,
        Guid? projectId = null,
        SearchSourceKind kind = SearchSourceKind.Item,
        Guid? sourceId = null,
        string? embedAs = null,
        int chunk = 0)
    {
        var builder = new SearchDocBuilder(projectId ?? SeedIds.Projects.Ship, kind, sourceId ?? itemId, field, text).WithWorkItem(itemId).WithChunk(chunk);
        if (embedded)
        {
            builder.WithEmbedding((await Embedder.EmbedPassagesAsync([embedAs ?? text]))[0], Embedder.ModelId!);
        }

        return await builder.InsertAsync(connection);
    }

    protected Task<CandidateLists> ListsAsync(string text, bool vector = true, params Guid[] projectIds)
        => ListsAsync(text, vector, projectIds, q => q);

    protected async Task<CandidateLists> ListsAsync(string text, bool vector, Guid[] projectIds, Func<CandidateQuery, CandidateQuery> adjust)
    {
        using var scope = Host.Services.CreateScope();
        var leg = scope.ServiceProvider.GetServices<IItemCandidates>().Single(c => c.Leg == CandidateLeg.Semantic);
        var query = new CandidateQuery(
            text,
            projectIds.Length == 0 ? [SeedIds.Projects.Ship] : projectIds,
            Types: null,
            Tags: null,
            IncludeClosed: true,
            Prefix: false,
            Criteria: null,
            vector ? await Embedder.EmbedQueryAsync(text) : null);
        return await leg.ListsAsync(adjust(query));
    }

    protected static IReadOnlyList<Guid> Ids(CandidateLists lists, RankedListName name)
        => [.. lists.Lists.Single(l => l.Name == name).Hits.Select(h => h.WorkItemId)];

    protected static Task ExecuteAsync(NpgsqlConnection connection, string sql, object parameters) => connection.ExecuteAsync(sql, parameters);
}
