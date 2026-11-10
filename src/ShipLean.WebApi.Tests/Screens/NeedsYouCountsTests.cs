using Npgsql;
using ShipLean.Application.Features.Backlog.NeedsYou;
using ShipLean.Contracts.Screens;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Screens;

// INeedsYouCounts against the seeded database. Every test arranges its own people and items, so the seeded asks (Tom's) never reach them.
[TestFixture]
public class NeedsYouCountsTests : ApiFixture
{
    private NpgsqlDataSource _dataSource = null!;

    [OneTimeSetUp]
    public void CreateDataSource() => _dataSource = NpgsqlDataSource.Create(ConnectionString);

    [OneTimeTearDown]
    public async Task DisposeDataSource() => await _dataSource.DisposeAsync();

    private Task<IReadOnlyDictionary<Guid, NeedsYouDto>> CountsAsync(Guid user, params Guid[] items) =>
        new NeedsYouCounts(_dataSource).ForItemsAsync(user, items, CancellationToken.None);

    [Test]
    public async Task ForItemsAsync_WhenOwnerHasOpenQuestionAndDecision_ShouldCountBoth()
    {
        await using var connection = await OpenDatabaseAsync();
        var owner = await NeedsYouArrangement.MemberAsync(connection);
        var item = await NeedsYouArrangement.ItemAsync(connection, "story", owner);
        await NeedsYouArrangement.AsksAsync(connection, item, questions: 2, decisions: 1);

        var counts = await CountsAsync(owner, item);

        counts.ShouldBe(new Dictionary<Guid, NeedsYouDto> { [item] = new NeedsYouDto(2, 1, false) });
    }

    [Test]
    public async Task ForItemsAsync_WhenAsksAnsweredOrWithdrawn_ShouldLeaveItemOut()
    {
        await using var connection = await OpenDatabaseAsync();
        var owner = await NeedsYouArrangement.MemberAsync(connection);
        var item = await NeedsYouArrangement.ItemAsync(connection, "story", owner);
        await new QuestionBuilder(item).Withdrawn(owner, TimeProvider.System.GetUtcNow()).BuildAsync(connection);
        await new DecisionBuilder(item).State("confirmed").AnsweredBy(owner, TimeProvider.System.GetUtcNow()).BuildAsync(connection);

        var counts = await CountsAsync(owner, item);

        counts.ShouldBeEmpty();
    }

    [Test]
    public async Task ForItemsAsync_WhenUnassigned_ShouldUseCreatorAsOwner()
    {
        await using var connection = await OpenDatabaseAsync();
        var creator = await NeedsYouArrangement.MemberAsync(connection);
        var other = await NeedsYouArrangement.MemberAsync(connection);
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithCreatedBy(creator).InsertAsync(connection);
        await NeedsYouArrangement.AsksAsync(connection, item, questions: 1, decisions: 0);

        (await CountsAsync(creator, item)).ShouldBe(new Dictionary<Guid, NeedsYouDto> { [item] = new NeedsYouDto(1, 0, false) });
        (await CountsAsync(other, item)).ShouldBeEmpty();
    }

    [Test]
    public async Task ForItemsAsync_WhenAnotherUserOwns_ShouldLeaveItemOut()
    {
        await using var connection = await OpenDatabaseAsync();
        var owner = await NeedsYouArrangement.MemberAsync(connection);
        var asker = await NeedsYouArrangement.MemberAsync(connection);
        var item = await NeedsYouArrangement.ItemAsync(connection, "story", owner);
        await NeedsYouArrangement.AsksAsync(connection, item, questions: 1, decisions: 1);

        var counts = await CountsAsync(asker, item);

        counts.ShouldBeEmpty();
    }

    [Test]
    public async Task ForItemsAsync_WhenStoryUnderFeatureHasAsks_ShouldRollUpWithOnChildren()
    {
        await using var connection = await OpenDatabaseAsync();
        var owner = await NeedsYouArrangement.MemberAsync(connection);
        var someoneElse = await NeedsYouArrangement.MemberAsync(connection);
        var feature = await NeedsYouArrangement.ItemAsync(connection, "feature", owner);
        var story = await NeedsYouArrangement.ItemAsync(connection, "story", owner, feature);
        var bug = await NeedsYouArrangement.ItemAsync(connection, "bug", owner, feature);
        var othersStory = await NeedsYouArrangement.ItemAsync(connection, "story", someoneElse, feature);
        var nested = await NeedsYouArrangement.ItemAsync(connection, "feature", owner, feature);
        var nestedStory = await NeedsYouArrangement.ItemAsync(connection, "story", owner, nested);
        await NeedsYouArrangement.AsksAsync(connection, feature, questions: 0, decisions: 1);
        await NeedsYouArrangement.AsksAsync(connection, story, questions: 2, decisions: 0);
        await NeedsYouArrangement.AsksAsync(connection, bug, questions: 1, decisions: 0);
        await NeedsYouArrangement.AsksAsync(connection, othersStory, questions: 5, decisions: 5);
        await NeedsYouArrangement.AsksAsync(connection, nestedStory, questions: 7, decisions: 0);

        var counts = await CountsAsync(owner, feature, story, nested);

        counts[feature].ShouldBe(new NeedsYouDto(3, 1, true));
        counts[story].ShouldBe(new NeedsYouDto(2, 0, false));
        counts[nested].ShouldBe(new NeedsYouDto(7, 0, true));
    }

    [Test]
    public async Task ForItemsAsync_WhenFeatureOnlyHasItsOwnAsks_ShouldNotMarkOnChildren()
    {
        await using var connection = await OpenDatabaseAsync();
        var owner = await NeedsYouArrangement.MemberAsync(connection);
        var feature = await NeedsYouArrangement.ItemAsync(connection, "feature", owner);
        await NeedsYouArrangement.ItemAsync(connection, "story", owner, feature);
        await NeedsYouArrangement.AsksAsync(connection, feature, questions: 1, decisions: 0);

        var counts = await CountsAsync(owner, feature);

        counts[feature].ShouldBe(new NeedsYouDto(1, 0, false));
    }

    [Test]
    public async Task ForItemsAsync_WhenUserLeftTheProject_ShouldCountNothing()
    {
        await using var connection = await OpenDatabaseAsync();
        var owner = await NeedsYouArrangement.MemberAsync(connection);
        var item = await NeedsYouArrangement.ItemAsync(connection, "story", owner);
        await NeedsYouArrangement.AsksAsync(connection, item, questions: 1, decisions: 1);
        await NeedsYouArrangement.LeaveAsync(connection, owner);

        var counts = await CountsAsync(owner, item);

        counts.ShouldBeEmpty();
    }

    [Test]
    public async Task ForItemsAsync_WhenItemRemoved_ShouldLeaveItemOut()
    {
        await using var connection = await OpenDatabaseAsync();
        var owner = await NeedsYouArrangement.MemberAsync(connection);
        var item = await NeedsYouArrangement.ItemAsync(connection, "story", owner, state: "Removed");
        await NeedsYouArrangement.AsksAsync(connection, item, questions: 1, decisions: 0);

        var counts = await CountsAsync(owner, item);

        counts.ShouldBeEmpty();
    }
}
