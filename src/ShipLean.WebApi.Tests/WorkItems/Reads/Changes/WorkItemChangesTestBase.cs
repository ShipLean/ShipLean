using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.WorkItems.Reads.Changes;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.WorkItems;
using ShipLean.WebApi.Tests.WorkItems.Store;

namespace ShipLean.WebApi.Tests.WorkItems.Reads.Changes;

// One seeded clone per class (from the store test base); every test arranges a project of its own and builds items,
// revisions and comments with the builders.
public abstract class WorkItemChangesTestBase : PostgresWorkItemStoreTestBase
{
    protected WorkItemChanges Changes { get; private set; } = null!;

    [OneTimeSetUp]
    public void CreateChanges() => Changes = new WorkItemChanges(NpgsqlDataSource.Create(DatabaseServices.PinUtc(Arrangement.Database.ConnectionString)));

    protected async Task<Guid> AddItemAsync(ArrangedProject project, Action<WorkItemBuilder>? configure = null)
    {
        var builder = new WorkItemBuilder(project.ProjectId);
        configure?.Invoke(builder);
        await using var connection = await Arrangement.Database.OpenAsync();
        return await builder.InsertAsync(connection);
    }

    protected async Task AddRevisionAsync(Guid itemId, int rev, DateTimeOffset at, Guid? by = null)
    {
        await using var connection = await Arrangement.Database.OpenAsync();
        await new RevisionBuilder(itemId).WithRev(rev).WithChangedAt(at).WithChangedBy(by).WithChanges("""[{"field":"points","from":null,"to":3,"fromLabel":null,"toLabel":null}]""").InsertAsync(connection);
    }

    protected async Task<Guid> AddCommentAsync(Guid itemId, string body, DateTimeOffset at, Guid? by = null)
    {
        var builder = new CommentBuilder(itemId).WithBody(body).WithCreatedAt(at);
        if (by is { } author)
        {
            builder.WithAuthor(author);
        }

        await using var connection = await Arrangement.Database.OpenAsync();
        return await builder.InsertAsync(connection);
    }
}
