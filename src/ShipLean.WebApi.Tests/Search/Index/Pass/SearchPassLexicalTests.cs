using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Search.Embedding;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Search;

namespace ShipLean.WebApi.Tests.Search.Index.Pass;

[TestFixture]
public class SearchPassLexicalTests : ApiFixture
{
    private readonly RecordingSearchDocWriter _writer = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.Replace(ServiceDescriptor.Singleton<IEmbedder>(new NoEmbedder("switched off")));
        services.Replace(ServiceDescriptor.Singleton<ISearchDocWriter>(_writer));
    };

    [Test]
    public async Task Pass_WhenEmbedderLexical_ShouldSkipEmbedding()
    {
        Guid id;
        await using (var connection = await OpenDatabaseAsync())
        {
            id = await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Sprint, Guid.NewGuid(), SearchField.SprintGoal, "Lexical goal").InsertAsync(connection);
        }

        IndexPassResult result;
        using (var scope = Host.Services.CreateScope())
        {
            result = await scope.ServiceProvider.GetRequiredService<ISearchIndexer>().RunPassAsync(true);
        }

        result.Embedded.ShouldBe(0);
        result.Remaining.ShouldBe(0);
        _writer.SyncAllCalls.ShouldBe(1);
        await using var check = await OpenDatabaseAsync();
        (await check.ExecuteScalarAsync<bool>("SELECT embedding IS NULL FROM search_doc WHERE id = @id", new { id })).ShouldBeTrue();
    }
}
