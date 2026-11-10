using Dapper;
using Npgsql;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Inbox;

namespace ShipLean.WebApi.Tests.Inbox;

// The Inbox's tables (K1: comment_mention, question.options) and the builders the Inbox's server tasks arrange with. The database is
// shared by the class, so every test arranges its own work item.
[TestFixture]
public class MentionSqlTests
{
    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task CommentMention_WhenSameCommentAndUserTwice_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        var comment = await new CommentBuilder(item).WithAuthor(SeedIds.Users.Priya).InsertAsync(connection);
        await new MentionBuilder(comment, item, SeedIds.Users.Tom).InsertAsync(connection);

        var error = await Should.ThrowAsync<PostgresException>(() => new MentionBuilder(comment, item, SeedIds.Users.Tom).WithState("seen").InsertAsync(connection));

        error.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        error.ConstraintName.ShouldBe("ux_comment_mention_comment_user");
    }

    [Test]
    public async Task CommentMention_WhenStateUnknown_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        var comment = await new CommentBuilder(item).WithAuthor(SeedIds.Users.Priya).InsertAsync(connection);

        var error = await Should.ThrowAsync<PostgresException>(() => new MentionBuilder(comment, item, SeedIds.Users.Tom).WithState("archived").InsertAsync(connection));

        error.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        error.ConstraintName.ShouldBe("comment_mention_state_check");
    }

    [Test]
    public async Task MentionBuilder_WhenInserted_ShouldReadBackThroughInboxRows()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        var comment = await new CommentBuilder(item).WithAuthor(SeedIds.Users.Priya).InsertAsync(connection);
        var reply = await new CommentBuilder(item).WithAuthor(SeedIds.Users.Tom).InsertAsync(connection);
        var id = Guid.NewGuid();
        var createdAt = TestDates.Today.AddDays(-2).AddHours(9);
        var closedAt = TestDates.Today.AddDays(-1).AddHours(10);

        await new MentionBuilder(comment, item, SeedIds.Users.Tom).WithId(id).WithState("replied").WithCreatedAt(createdAt).WithClosedAt(closedAt)
            .WithReply(reply).InsertAsync(connection);

        var mention = (await InboxRows.MentionsAsync(connection, comment)).ShouldHaveSingleItem();
        mention.ShouldBe(new InboxRows.Mention(id, comment, item, SeedIds.Users.Tom, "replied", createdAt, closedAt, reply));
    }

    [Test]
    public async Task QuestionOptions_WhenMoreThanSix_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        await new QuestionBuilder(item).WithOptions("1", "2", "3", "4", "5", "6").BuildAsync(connection);

        var error = await Should.ThrowAsync<PostgresException>(() => new QuestionBuilder(item).WithOptions("1", "2", "3", "4", "5", "6", "7").BuildAsync(connection));

        error.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        error.ConstraintName.ShouldBe("ck_question_options");
    }

    [Test]
    public async Task QuestionOptions_WhenNotGiven_ShouldDefaultToEmptyArray()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        var id = Guid.NewGuid();

        await connection.ExecuteAsync(
            "INSERT INTO question (id, work_item_id, number, text, created_by_user_id) VALUES (@id, @item, 1, 'Plain question?', @joel)",
            new { id, item, joel = SeedIds.Users.Joel });

        (await connection.ExecuteScalarAsync<string>("SELECT options::text FROM question WHERE id = @id", new { id })).ShouldBe("[]");
        (await InboxRows.QuestionOptionsAsync(connection, id)).ShouldBeEmpty();
    }

    [Test]
    public async Task QuestionBuilder_WhenWithOptions_ShouldReadBackInOrder()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);

        var (id, _) = await new QuestionBuilder(item).WithOptions("50 MB", "20 MB", "100 MB").BuildAsync(connection);

        (await InboxRows.QuestionOptionsAsync(connection, id)).ShouldBe(["50 MB", "20 MB", "100 MB"]);
    }

    [Test]
    public async Task WorkItemBuilder_WhenWithStatusLine_ShouldWriteVerifiedColumns()
    {
        await using var connection = await _database.OpenAsync();
        var verifiedAt = TestDates.Today.AddDays(-4).AddHours(15);

        var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithStatusLine("Waiting on the API", verifiedAt, SeedIds.Users.Sam, "Claude Code")
            .InsertAsync(connection);

        var row = await connection.QuerySingleAsync<(string Line, DateTimeOffset At, Guid By, string Agent)>(
            "SELECT status_line, status_verified_at, status_verified_by, status_verified_agent FROM work_item WHERE id = @id", new { id });
        row.ShouldBe(("Waiting on the API", verifiedAt, SeedIds.Users.Sam, "Claude Code"));
    }

    [Test]
    public async Task WorkItemBuilder_WhenWithCreatedBy_ShouldWriteCreatedBy()
    {
        await using var connection = await _database.OpenAsync();
        var createdAt = TestDates.Today.AddDays(-9).AddHours(8);

        var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithCreatedBy(SeedIds.Users.Mei).WithCreatedAt(createdAt).InsertAsync(connection);

        var row = await connection.QuerySingleAsync<(Guid By, DateTimeOffset At)>("SELECT created_by, created_at FROM work_item WHERE id = @id", new { id });
        row.ShouldBe((SeedIds.Users.Mei, createdAt));
    }

    [Test]
    public async Task CommentBuilder_WhenWithAgentName_ShouldWriteAgentName()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);

        var id = await new CommentBuilder(item).WithAuthor(SeedIds.Users.Tom).WithAgentName("Claude Code").InsertAsync(connection);

        (await connection.ExecuteScalarAsync<string>("SELECT agent_name FROM comment WHERE id = @id", new { id })).ShouldBe("Claude Code");
    }

    // Reads the row as JSON, so it passes whether or not the health columns exist yet.
    [Test]
    public async Task WorkItemBuilder_WhenNoHealthGiven_ShouldInsertWithoutHealthColumns()
    {
        await using var connection = await _database.OpenAsync();

        var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").InsertAsync(connection);

        (await connection.ExecuteScalarAsync<string?>("SELECT to_jsonb(w) ->> 'health' FROM work_item w WHERE w.id = @id", new { id })).ShouldBeNull();
    }
}
