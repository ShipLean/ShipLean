using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Search.Index.Writer;

// SyncAllAsync asks every registered text source for its ids and texts. The sources are recording doubles, one per kind, so the
// test covers the writer's loop and not the real sources (B2c has their tests).
[TestFixture]
public class SearchDocWriterSyncAllTests : ApiFixture
{
    private static readonly Guid Project = SeedIds.Projects.Ship;
    private static readonly Guid Item = Guid.NewGuid();
    private static readonly Guid Comment = Guid.NewGuid();
    private static readonly Guid Attachment = Guid.NewGuid();
    private static readonly Guid ProjectSource = Project;
    private static readonly Guid Sprint = Guid.NewGuid();

    private readonly List<RecordingTextSource> _sources =
    [
        new(SearchSourceKind.Item, new SearchSourceText(Item, Project, Item, "Item title", [new(SearchField.Title, "Item title"), new(SearchField.Body, "Item body")])),
        new(SearchSourceKind.Comment, new SearchSourceText(Comment, Project, Item, "Item title", [new(SearchField.Comment, "A comment")])),
        new(SearchSourceKind.Attachment, new SearchSourceText(Attachment, Project, Item, "Item title", [new(SearchField.Attachment, "Attachment text")])),
        new(SearchSourceKind.Project, new SearchSourceText(ProjectSource, Project, null, null, [new(SearchField.Project, "SHIP Storefront\nDescription")])),
        new(SearchSourceKind.Sprint, new SearchSourceText(Sprint, Project, null, null, [new(SearchField.SprintName, "Sprint 99"), new(SearchField.SprintGoal, "Ship it")])),
    ];

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<ISearchTextSource>();
        foreach (var source in _sources)
        {
            services.AddSingleton<ISearchTextSource>(source);
        }
    };

    [OneTimeSetUp]
    public async Task ArrangeItem()
    {
        await using var connection = await OpenDatabaseAsync();
        await new WorkItemBuilder(Project).WithId(Item).WithTitle("Item title").InsertAsync(connection);
        // A leftover row of an item the sources no longer know: SyncAll drops it.
        var gone = await new WorkItemBuilder(Project).WithTitle("Forgotten").InsertAsync(connection);
        await new SearchDocBuilder(Project, SearchSourceKind.Item, gone, SearchField.Title, "Forgotten").WithWorkItem(gone).InsertAsync(connection);
    }

    [Test]
    public async Task Index_WhenSyncAll_ShouldCoverEverySourceKind()
    {
        using (var scope = Host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISearchDocWriter>().SyncAllAsync();
        }

        _sources.ShouldAllBe(s => s.AllIdsCalls == 1);
        await using var connection = await OpenDatabaseAsync();
        var rows = (await connection.QueryAsync<(string Kind, Guid SourceId, string Field, Guid? WorkItemId)>(
            "SELECT source_kind, source_id, field, work_item_id FROM search_doc ORDER BY source_kind, field")).ToList();
        rows.ShouldBe(
        [
            ("attachment", Attachment, "attachment", Item),
            ("comment", Comment, "comment", Item),
            ("item", Item, "body", Item),
            ("item", Item, "title", Item),
            ("project", ProjectSource, "project", null),
            ("sprint", Sprint, "sprint_goal", null),
            ("sprint", Sprint, "sprint_name", null),
        ]);
        // The recording sources' texts are prefixed by the writer, not by the source.
        (await connection.ExecuteScalarAsync<string>("SELECT text FROM search_doc WHERE source_id = @Comment", new { Comment })).ShouldBe("Item title\nA comment");
        (await connection.ExecuteScalarAsync<string>("SELECT text FROM search_doc WHERE source_id = @Sprint AND field = 'sprint_goal'", new { Sprint })).ShouldBe("Ship it");
    }
}
