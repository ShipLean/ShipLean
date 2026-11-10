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

// AskInboxSource: the open decisions and questions on items the user owns. The database is shared by the class, so every test
// arranges its own users and work items; the user under test is a fresh member of the projects the test uses.
[TestFixture]
public class AskInboxSourceTests
{
    private FreshDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;
    private FakeTimeProvider _clock = null!;
    private AskInboxSource _source = null!;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        _database = await FreshDatabase.CreateAsync();
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_database.ConnectionString));
        _clock = new FakeTimeProvider(AskDefaults.Now().AddHours(1));
        _source = new AskInboxSource(_dataSource, _clock);
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Test]
    public async Task OpenAsync_WhenDecisionAndQuestionOpenOnOwnedItem_ShouldListBoth()
    {
        await using var connection = await _database.OpenAsync();
        var owner = await MemberAsync(connection, SeedIds.Projects.Ship);
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithAssignee(owner).InsertAsync(connection);
        var raisedAt = _clock.GetUtcNow().AddDays(-2);
        await new DecisionBuilder(item).Question("Which store?").Recommended("Postgres").Alternatives("Redis", "Files")
            .RaisedBy(SeedIds.Users.Priya, "Claude Code", raisedAt).BuildAsync(connection);
        await new QuestionBuilder(item).Text("Ship it Friday?").AskedBy(SeedIds.Users.Mei, null, raisedAt.AddHours(1)).BuildAsync(connection);

        var rows = await _source.OpenAsync(owner, SeedIds.Projects.Ship);

        rows.Count.ShouldBe(2);
        var decision = rows.Single(r => r.Ask.Kind == InboxAskKind.Decision);
        decision.WorkItemId.ShouldBe(item);
        decision.Ask.Label.ShouldBe("D1");
        decision.Ask.At.ShouldBe(raisedAt);
        decision.Ask.AtLabel.ShouldBe(DisplayDate.Relative(raisedAt, _clock));
        decision.Ask.From!.Id.ShouldBe(SeedIds.Users.Priya);
        decision.Ask.AgentName.ShouldBe("Claude Code");
        decision.Ask.Decision.ShouldBe(new InboxDecisionDto(1, "Which store?", "Postgres", ["Redis", "Files"]));
        decision.Ask.Question.ShouldBeNull();
        var question = rows.Single(r => r.Ask.Kind == InboxAskKind.Question);
        question.Ask.Label.ShouldBe("Q1");
        question.Ask.From!.Id.ShouldBe(SeedIds.Users.Mei);
        question.Ask.AgentName.ShouldBeNull();
        question.Ask.Question!.Text.ShouldBe("Ship it Friday?");
        question.Ask.Decision.ShouldBeNull();
    }

    [Test]
    public async Task OpenAsync_WhenAsksConfirmedChangedWithdrawnOrAnswered_ShouldLeaveThemOut()
    {
        await using var connection = await _database.OpenAsync();
        var owner = await MemberAsync(connection, SeedIds.Projects.Ship);
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithAssignee(owner).InsertAsync(connection);
        await new DecisionBuilder(item).State("confirmed").BuildAsync(connection);
        await new DecisionBuilder(item).State("changed").Answer("own", "Something else", null).BuildAsync(connection);
        await new DecisionBuilder(item).State("withdrawn").BuildAsync(connection);
        var comment = await new CommentBuilder(item).WithAuthor(owner).InsertAsync(connection);
        await new QuestionBuilder(item).Answered(comment, owner, _clock.GetUtcNow()).BuildAsync(connection);
        await new QuestionBuilder(item).Withdrawn(SeedIds.Users.Joel, _clock.GetUtcNow()).BuildAsync(connection);

        (await _source.OpenAsync(owner, SeedIds.Projects.Ship)).ShouldBeEmpty();
        (await _source.OpenCountsByProjectAsync(owner)).ShouldBeEmpty();
    }

    [Test]
    public async Task OpenAsync_WhenItemUnassigned_ShouldUseCreatorAsOwner()
    {
        await using var connection = await _database.OpenAsync();
        var creator = await MemberAsync(connection, SeedIds.Projects.Ship);
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithCreatedBy(creator).InsertAsync(connection);
        await new DecisionBuilder(item).BuildAsync(connection);

        var rows = await _source.OpenAsync(creator, SeedIds.Projects.Ship);

        rows.Single().WorkItemId.ShouldBe(item);
    }

    [Test]
    public async Task OpenAsync_WhenAnotherPersonOwnsItem_ShouldLeaveItOut()
    {
        await using var connection = await _database.OpenAsync();
        var creator = await MemberAsync(connection, SeedIds.Projects.Ship);
        var assignee = await MemberAsync(connection, SeedIds.Projects.Ship);
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithCreatedBy(creator).WithAssignee(assignee)
            .InsertAsync(connection);
        await new DecisionBuilder(item).BuildAsync(connection);

        (await _source.OpenAsync(creator, SeedIds.Projects.Ship)).ShouldBeEmpty();
        (await _source.OpenAsync(assignee, SeedIds.Projects.Ship)).Count.ShouldBe(1);
    }

    [Test]
    public async Task OpenAsync_WhenItemRemoved_ShouldLeaveItOut()
    {
        await using var connection = await _database.OpenAsync();
        var owner = await MemberAsync(connection, SeedIds.Projects.Ship);
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithAssignee(owner)
            .WithState(SeedIds.States.Ship.StoryRemoved).InsertAsync(connection);
        await new QuestionBuilder(item).BuildAsync(connection);

        (await _source.OpenAsync(owner, SeedIds.Projects.Ship)).ShouldBeEmpty();
        (await _source.OpenCountsByProjectAsync(owner)).ShouldBeEmpty();
    }

    [Test]
    public async Task OpenAsync_WhenMembershipEnded_ShouldLeaveItOut()
    {
        await using var connection = await _database.OpenAsync();
        var leaver = await MemberAsync(connection, SeedIds.Projects.Ship);
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithAssignee(leaver).InsertAsync(connection);
        await new DecisionBuilder(item).BuildAsync(connection);
        (await _source.OpenAsync(leaver, SeedIds.Projects.Ship)).Count.ShouldBe(1);

        await connection.ExecuteAsync(
            "UPDATE project_member SET archived_at = now() WHERE project_id = @project AND user_id = @leaver",
            new { project = SeedIds.Projects.Ship, leaver });

        (await _source.OpenAsync(leaver, SeedIds.Projects.Ship)).ShouldBeEmpty();
    }

    [Test]
    public async Task OpenAsync_WhenQuestionHasOptions_ShouldCarryThem()
    {
        await using var connection = await _database.OpenAsync();
        var owner = await MemberAsync(connection, SeedIds.Projects.Ship);
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithAssignee(owner).InsertAsync(connection);
        await new QuestionBuilder(item).Text("Which?").WithOptions("Yes", "No", "Later").BuildAsync(connection);
        await new QuestionBuilder(item).Text("Why?").BuildAsync(connection);

        var questions = (await _source.OpenAsync(owner, SeedIds.Projects.Ship)).Select(r => r.Ask.Question!).ToList();

        questions.Single(q => q.Text == "Which?").Options.ShouldBe(["Yes", "No", "Later"]);
        questions.Single(q => q.Text == "Why?").Options.ShouldBeEmpty();
    }

    [Test]
    public async Task OpenCountsByProjectAsync_WhenAsksInTwoProjects_ShouldCountEach()
    {
        await using var connection = await _database.OpenAsync();
        var owner = await MemberAsync(connection, SeedIds.Projects.Ship, SeedIds.Projects.Pay);
        var shipItem = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithAssignee(owner).InsertAsync(connection);
        var payItem = await new WorkItemBuilder(SeedIds.Projects.Pay).WithType("story").WithAssignee(owner).InsertAsync(connection);
        await new DecisionBuilder(shipItem).BuildAsync(connection);
        await new QuestionBuilder(shipItem).BuildAsync(connection);
        await new QuestionBuilder(payItem).BuildAsync(connection);

        var counts = await _source.OpenCountsByProjectAsync(owner);

        counts.Count.ShouldBe(2);
        counts[SeedIds.Projects.Ship].ShouldBe(2);
        counts[SeedIds.Projects.Pay].ShouldBe(1);
    }

    [Test]
    public async Task EverAsync_WhenEveryAskAnswered_ShouldStayTrue()
    {
        await using var connection = await _database.OpenAsync();
        var owner = await MemberAsync(connection, SeedIds.Projects.Ship);
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithAssignee(owner).InsertAsync(connection);
        await new DecisionBuilder(item).State("confirmed").BuildAsync(connection);

        (await _source.OpenAsync(owner, SeedIds.Projects.Ship)).ShouldBeEmpty();
        (await _source.EverAsync(owner)).ShouldBeTrue();
    }

    [Test]
    public async Task EverAsync_WhenNeverAsked_ShouldBeFalse()
    {
        await using var connection = await _database.OpenAsync();
        var owner = await MemberAsync(connection, SeedIds.Projects.Ship);
        var other = await MemberAsync(connection, SeedIds.Projects.Ship);
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithAssignee(owner).InsertAsync(connection);
        var othersItem = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithAssignee(other).InsertAsync(connection);
        await new QuestionBuilder(othersItem).BuildAsync(connection);

        (await _source.EverAsync(owner)).ShouldBeFalse();
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
