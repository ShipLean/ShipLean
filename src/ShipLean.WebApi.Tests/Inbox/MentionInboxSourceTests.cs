using Dapper;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.Inbox;
using ShipLean.Contracts.Inbox;
using ShipLean.Core;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Asks;
using ShipLean.TestSupport.Builders;

namespace ShipLean.WebApi.Tests.Inbox;

// MentionInboxSource: the user's open @mentions on live comments and items in projects they still belong to. The database is shared
// by the class and the seed already mentions Tom, so every test arranges its own user, items and comments.
[TestFixture]
public class MentionInboxSourceTests
{
    private FreshDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;
    private FakeTimeProvider _clock = null!;
    private MentionInboxSource _source = null!;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        _database = await FreshDatabase.CreateAsync();
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_database.ConnectionString));
        _clock = new FakeTimeProvider(AskDefaults.Now().AddHours(1));
        _source = new MentionInboxSource(_dataSource, _clock);
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Test]
    public async Task OpenAsync_WhenMentionOpen_ShouldListWithExcerptCutOnAWord()
    {
        await using var connection = await _database.OpenAsync();
        var me = await MemberAsync(connection, SeedIds.Projects.Ship);
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        // Plain text is "Tom, please check this: " (24 characters) then nine-letter words, so the 25th word ends at 272 and the
        // 26th would pass 279, the room left for "…".
        var words = Enumerable.Repeat("abcdefghi", 40).ToList();
        var comment = await new CommentBuilder(item).WithAuthor(SeedIds.Users.Priya)
            .WithBody($"**Tom**, please   check this:\n\n{string.Join(' ', words)}").InsertAsync(connection);
        var at = _clock.GetUtcNow().AddHours(-5);
        var mention = await new MentionBuilder(comment, item, me).WithCreatedAt(at).InsertAsync(connection);

        var rows = await _source.OpenAsync(me, SeedIds.Projects.Ship);

        var row = rows.Single();
        row.WorkItemId.ShouldBe(item);
        row.Ask.Kind.ShouldBe(InboxAskKind.Mention);
        row.Ask.Label.ShouldBe("@");
        row.Ask.At.ShouldBe(at);
        row.Ask.AtLabel.ShouldBe(DisplayDate.Relative(at, _clock));
        row.Ask.From!.Id.ShouldBe(SeedIds.Users.Priya);
        row.Ask.AgentName.ShouldBeNull();
        row.Ask.Decision.ShouldBeNull();
        row.Ask.Question.ShouldBeNull();
        row.Ask.Status.ShouldBeNull();
        var expected = "Tom, please check this: " + string.Join(' ', words.Take(25)) + "…";
        row.Ask.Mention.ShouldBe(new InboxMentionDto(mention, comment, expected));
        expected.Length.ShouldBeLessThanOrEqualTo(InboxRules.ExcerptLength);
    }

    [Test]
    public async Task OpenAsync_WhenMentionSeenRepliedOrWithdrawn_ShouldLeaveItOut()
    {
        await using var connection = await _database.OpenAsync();
        var me = await MemberAsync(connection, SeedIds.Projects.Ship);
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        var open = await MentionAsync(connection, item, me);
        foreach (var state in new[] { "seen", "replied", "withdrawn" })
        {
            await MentionAsync(connection, item, me, state);
        }

        (await _source.OpenAsync(me, SeedIds.Projects.Ship)).Single().Ask.Mention!.MentionId.ShouldBe(open);
        (await _source.OpenCountsByProjectAsync(me)).ShouldBe(new Dictionary<Guid, int> { [SeedIds.Projects.Ship] = 1 });
    }

    [Test]
    public async Task OpenAsync_WhenCommentArchivedOrItemRemoved_ShouldLeaveItOut()
    {
        await using var connection = await _database.OpenAsync();
        var me = await MemberAsync(connection, SeedIds.Projects.Ship);
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        var removed = await new WorkItemBuilder(SeedIds.Projects.Ship).WithState(SeedIds.States.Ship.StoryRemoved).InsertAsync(connection);
        var open = await MentionAsync(connection, item, me);
        var archivedComment = await new CommentBuilder(item).WithAuthor(SeedIds.Users.Priya).Archived().InsertAsync(connection);
        await new MentionBuilder(archivedComment, item, me).InsertAsync(connection);
        await MentionAsync(connection, removed, me);

        (await _source.OpenAsync(me, SeedIds.Projects.Ship)).Single().Ask.Mention!.MentionId.ShouldBe(open);
        (await _source.OpenCountsByProjectAsync(me)).ShouldBe(new Dictionary<Guid, int> { [SeedIds.Projects.Ship] = 1 });
    }

    [Test]
    public async Task OpenAsync_WhenMentionIsSomeoneElses_ShouldLeaveItOut()
    {
        await using var connection = await _database.OpenAsync();
        var me = await MemberAsync(connection, SeedIds.Projects.Ship);
        var someoneElse = await MemberAsync(connection, SeedIds.Projects.Ship);
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        var theirs = await MentionAsync(connection, item, someoneElse);

        (await _source.OpenAsync(me, SeedIds.Projects.Ship)).ShouldBeEmpty();
        (await _source.OpenCountsByProjectAsync(me)).ShouldBeEmpty();
        (await _source.OpenAsync(someoneElse, SeedIds.Projects.Ship)).Single().Ask.Mention!.MentionId.ShouldBe(theirs);
    }

    [Test]
    public async Task OpenAsync_WhenUserLeftProject_ShouldLeaveItOut()
    {
        await using var connection = await _database.OpenAsync();
        var me = await MemberAsync(connection, SeedIds.Projects.Ship);
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        await MentionAsync(connection, item, me);
        await connection.ExecuteAsync(
            "UPDATE project_member SET archived_at = now() WHERE user_id = @me AND project_id = @project",
            new { me, project = SeedIds.Projects.Ship });

        (await _source.OpenAsync(me, SeedIds.Projects.Ship)).ShouldBeEmpty();
        (await _source.OpenCountsByProjectAsync(me)).ShouldBeEmpty();
    }

    [Test]
    public async Task OpenAsync_WhenAgentWroteComment_ShouldCarryAgentName()
    {
        await using var connection = await _database.OpenAsync();
        var me = await MemberAsync(connection, SeedIds.Projects.Ship);
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        var comment = await new CommentBuilder(item).WithAuthor(SeedIds.Users.Priya).WithAgentName("Claude Code (claude.ai)")
            .WithBody("Can you confirm the rollout date?").InsertAsync(connection);
        await new MentionBuilder(comment, item, me).InsertAsync(connection);

        var ask = (await _source.OpenAsync(me, SeedIds.Projects.Ship)).Single().Ask;

        ask.AgentName.ShouldBe("Claude Code (claude.ai)");
        ask.From!.Id.ShouldBe(SeedIds.Users.Priya);
        ask.Mention!.Excerpt.ShouldBe("Can you confirm the rollout date?");
    }

    [Test]
    public async Task OpenCountsByProjectAsync_WhenMentionsInTwoProjects_ShouldCountEach()
    {
        await using var connection = await _database.OpenAsync();
        var me = await MemberAsync(connection, SeedIds.Projects.Ship, SeedIds.Projects.Pay);
        var shipItem = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        var payItem = await new WorkItemBuilder(SeedIds.Projects.Pay).InsertAsync(connection);
        await MentionAsync(connection, shipItem, me);
        await MentionAsync(connection, shipItem, me);
        await MentionAsync(connection, payItem, me);
        await MentionAsync(connection, payItem, me, "seen");

        var counts = await _source.OpenCountsByProjectAsync(me);

        counts.ShouldBe(new Dictionary<Guid, int> { [SeedIds.Projects.Ship] = 2, [SeedIds.Projects.Pay] = 1 });
        (await _source.OpenAsync(me, SeedIds.Projects.Pay)).Single().WorkItemId.ShouldBe(payItem);
    }

    [Test]
    public async Task EverAsync_WhenOnlySeenMentions_ShouldBeTrue()
    {
        await using var connection = await _database.OpenAsync();
        var me = await MemberAsync(connection, SeedIds.Projects.Ship);
        var never = await MemberAsync(connection, SeedIds.Projects.Ship);
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        await MentionAsync(connection, item, me, "seen");

        (await _source.OpenAsync(me, SeedIds.Projects.Ship)).ShouldBeEmpty();
        (await _source.EverAsync(me)).ShouldBeTrue();
        (await _source.EverAsync(never)).ShouldBeFalse();
    }

    // A comment by Priya on the item that mentions user, in the given state.
    private static async Task<Guid> MentionAsync(NpgsqlConnection connection, Guid item, Guid user, string state = "open")
    {
        var comment = await new CommentBuilder(item).WithAuthor(SeedIds.Users.Priya).InsertAsync(connection);
        return await new MentionBuilder(comment, item, user).WithState(state).InsertAsync(connection);
    }

    private static async Task<Guid> MemberAsync(NpgsqlConnection connection, params Guid[] projects)
    {
        var user = await new AppUserBuilder().InsertAsync(connection);
        foreach (var project in projects)
        {
            await connection.ExecuteAsync(
                "INSERT INTO project_member (id, project_id, user_id) VALUES (@id, @project, @user)",
                new { id = Guid.NewGuid(), project, user });
        }
        return user;
    }
}
