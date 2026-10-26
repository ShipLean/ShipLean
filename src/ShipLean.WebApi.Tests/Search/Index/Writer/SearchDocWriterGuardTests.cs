using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Search.Index.Writer;

// A full sync must not delete what an event sync wrote after its id snapshot. One recording item source stands in for the real one.
[TestFixture]
public class SearchDocWriterGuardTests : ApiFixture
{
    private static readonly Guid Project = SeedIds.Projects.Ship;
    private static readonly Guid Item = Guid.NewGuid();

    private readonly RecordingTextSource _items = new(SearchSourceKind.Item,
        new SearchSourceText(Item, Project, Item, "Guard title", [new(SearchField.Title, "Guard title")]));

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<ISearchTextSource>();
        services.AddSingleton<ISearchTextSource>(_items);
    };

    [OneTimeSetUp]
    public async Task ArrangeItem()
    {
        await using var connection = await OpenDatabaseAsync();
        await new WorkItemBuilder(Project).WithId(Item).WithTitle("Guard title").InsertAsync(connection);
    }

    [Test]
    public async Task Index_WhenRowWrittenDuringSyncAll_ShouldKeepIt()
    {
        // A row for an id the source does not return, stamped after the pass's snapshot (the clock is read before the ids).
        var late = Guid.NewGuid();
        var old = Guid.NewGuid();
        await using (var connection = await OpenDatabaseAsync())
        {
            await new SearchDocBuilder(Project, SearchSourceKind.Item, late, SearchField.Title, "Written late").WithIndexedAt(new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero)).InsertAsync(connection);
            await new SearchDocBuilder(Project, SearchSourceKind.Item, old, SearchField.Title, "Written long ago").WithIndexedAt(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)).InsertAsync(connection);
        }

        using (var scope = Host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISearchDocWriter>().SyncAllAsync();
        }

        await using var check = await OpenDatabaseAsync();
        (await check.ExecuteScalarAsync<int>("SELECT count(*) FROM search_doc WHERE source_id = @late", new { late })).ShouldBe(1);
        (await check.ExecuteScalarAsync<int>("SELECT count(*) FROM search_doc WHERE source_id = @old", new { old })).ShouldBe(0);
    }
}
