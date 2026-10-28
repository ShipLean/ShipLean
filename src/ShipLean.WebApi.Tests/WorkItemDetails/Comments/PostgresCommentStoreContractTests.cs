using ShipLean.WebApi.Tests.Boards.Settings;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.WorkItemDetails.Comments;
using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Comments;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Comments;
using ShipLean.TestSupport.Database;
using ShipLean.WebApi.Tests.WorkItems.Store;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Comments;

// CommentStoreContract on Postgres: one seeded clone for the class, a new Ship story for every test.
[TestFixture]
public class PostgresCommentStoreContractTests : CommentStoreContract
{
    private ClonedDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;

    [OneTimeSetUp]
    public async Task CreateDatabase()
    {
        _database = await ShipLeanDatabase.CloneSeedAsync();
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_database.ConnectionString));
    }

    [OneTimeTearDown]
    public async Task DropDatabase()
    {
        await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override ICommentStore CreateStore(Guid actorId) => CreateStore(actorId, AgentIdentity.None);

    protected override ICommentStore CreateStore(Guid actorId, string agentName, string? agentHost, bool agentVerified)
        => CreateStore(actorId, new AgentIdentity(agentName, agentHost, agentVerified));

    protected override async Task<Guid> ArrangeWorkItemAsync()
    {
        await using var connection = await _database.OpenAsync();
        return await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
    }

    private CommentStore CreateStore(Guid actorId, ICurrentAgent agent)
        => new(_dataSource, new FixedCurrentUser(actorId), agent, new MarkdownRenderer(), TimeProvider.System, new DiscardingProjectEvents());
}
