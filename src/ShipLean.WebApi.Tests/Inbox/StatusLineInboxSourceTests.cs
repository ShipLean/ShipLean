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

// StatusLineInboxSource: status lines on the user's Features that were never verified or were last verified more than three days
// ago. The database is shared by the class, so every test arranges its own users and Features.
[TestFixture]
public class StatusLineInboxSourceTests
{
    private const string Line = "Key rotation is next; nothing blocked.";

    private FreshDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;
    private FakeTimeProvider _clock = null!;
    private StatusLineInboxSource _source = null!;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        _database = await FreshDatabase.CreateAsync();
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_database.ConnectionString));
        _clock = new FakeTimeProvider(AskDefaults.Now().AddHours(1));
        _source = new StatusLineInboxSource(_dataSource, _clock);
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Test]
    public async Task OpenAsync_WhenVerifiedFourDaysAgo_ShouldListWithVerifierAsFrom()
    {
        await using var connection = await _database.OpenAsync();
        var owner = await MemberAsync(connection, SeedIds.Projects.Ship);
        var verifiedAt = _clock.GetUtcNow().AddDays(-4);
        var feature = await FeatureAsync(connection, owner, verifiedAt, SeedIds.Users.Priya, "Claude Code");

        var rows = await _source.OpenAsync(owner, SeedIds.Projects.Ship);

        var row = rows.Single();
        row.WorkItemId.ShouldBe(feature);
        row.Ask.Kind.ShouldBe(InboxAskKind.Status);
        row.Ask.Label.ShouldBe("Status");
        row.Ask.At.ShouldBe(verifiedAt);
        row.Ask.AtLabel.ShouldBe(DisplayDate.Relative(verifiedAt, _clock));
        row.Ask.From!.Id.ShouldBe(SeedIds.Users.Priya);
        row.Ask.AgentName.ShouldBe("Claude Code");
        row.Ask.Status.ShouldBe(new InboxStatusDto(Line, verifiedAt));
        row.Ask.Decision.ShouldBeNull();
    }

    [Test]
    public async Task OpenAsync_WhenVerifiedToday_ShouldLeaveItOut()
    {
        await using var connection = await _database.OpenAsync();
        var owner = await MemberAsync(connection, SeedIds.Projects.Ship);
        await FeatureAsync(connection, owner, _clock.GetUtcNow(), SeedIds.Users.Priya, null);

        (await _source.OpenAsync(owner, SeedIds.Projects.Ship)).ShouldBeEmpty();
        (await _source.OpenCountsByProjectAsync(owner)).ShouldBeEmpty();
    }

    [Test]
    public async Task OpenAsync_WhenVerifiedExactlyThreeDaysAgo_ShouldLeaveItOut()
    {
        await using var connection = await _database.OpenAsync();
        var owner = await MemberAsync(connection, SeedIds.Projects.Ship);
        await FeatureAsync(connection, owner, _clock.GetUtcNow() - InboxRules.StatusStaleAfter, SeedIds.Users.Priya, null);

        (await _source.OpenAsync(owner, SeedIds.Projects.Ship)).ShouldBeEmpty();
    }

    [Test]
    public async Task OpenAsync_WhenNeverVerified_ShouldListAtCreatedAt()
    {
        await using var connection = await _database.OpenAsync();
        var owner = await MemberAsync(connection, SeedIds.Projects.Ship);
        var createdAt = _clock.GetUtcNow().AddDays(-10);
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithAssignee(owner).WithCreatedAt(createdAt)
            .WithStatusLine(Line, null, null, null).InsertAsync(connection);

        var row = (await _source.OpenAsync(owner, SeedIds.Projects.Ship)).Single();

        row.Ask.At.ShouldBe(createdAt);
        row.Ask.AtLabel.ShouldBe(DisplayDate.Relative(createdAt, _clock));
        row.Ask.From.ShouldBeNull();
        row.Ask.AgentName.ShouldBeNull();
        row.Ask.Status!.VerifiedAt.ShouldBeNull();
    }

    [Test]
    public async Task OpenAsync_WhenFeatureCompleted_ShouldLeaveItOut()
    {
        await using var connection = await _database.OpenAsync();
        var owner = await MemberAsync(connection, SeedIds.Projects.Ship);
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithAssignee(owner).WithState(SeedIds.States.Ship.FeatureClosed)
            .WithStatusLine(Line, _clock.GetUtcNow().AddDays(-9), SeedIds.Users.Priya, null).InsertAsync(connection);

        (await _source.OpenAsync(owner, SeedIds.Projects.Ship)).ShouldBeEmpty();
    }

    [Test]
    public async Task OpenAsync_WhenStatusLineEmpty_ShouldLeaveItOut()
    {
        await using var connection = await _database.OpenAsync();
        var owner = await MemberAsync(connection, SeedIds.Projects.Ship);
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithAssignee(owner).InsertAsync(connection);
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithAssignee(owner)
            .WithStatusLine("", null, null, null).InsertAsync(connection);

        (await _source.OpenAsync(owner, SeedIds.Projects.Ship)).ShouldBeEmpty();
        (await _source.EverAsync(owner)).ShouldBeFalse();
    }

    [Test]
    public async Task OpenAsync_WhenAnotherPersonOwnsFeature_ShouldLeaveItOut()
    {
        await using var connection = await _database.OpenAsync();
        var creator = await MemberAsync(connection, SeedIds.Projects.Ship);
        var assignee = await MemberAsync(connection, SeedIds.Projects.Ship);
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithCreatedBy(creator).WithAssignee(assignee)
            .WithStatusLine(Line, null, null, null).InsertAsync(connection);

        (await _source.OpenAsync(creator, SeedIds.Projects.Ship)).ShouldBeEmpty();
        (await _source.OpenAsync(assignee, SeedIds.Projects.Ship)).Count.ShouldBe(1);
    }

    [Test]
    public async Task OpenCountsByProjectAsync_WhenStaleLinesInTwoProjects_ShouldCountEach()
    {
        await using var connection = await _database.OpenAsync();
        var owner = await MemberAsync(connection, SeedIds.Projects.Ship, SeedIds.Projects.Pay);
        var stale = _clock.GetUtcNow().AddDays(-5);
        await FeatureAsync(connection, owner, stale, SeedIds.Users.Priya, null);
        await FeatureAsync(connection, owner, null, null, null);
        await FeatureAsync(connection, owner, _clock.GetUtcNow(), SeedIds.Users.Priya, null);
        await FeatureAsync(connection, owner, stale, SeedIds.Users.Priya, null, SeedIds.Projects.Pay);

        var counts = await _source.OpenCountsByProjectAsync(owner);

        counts.Count.ShouldBe(2);
        counts[SeedIds.Projects.Ship].ShouldBe(2);
        counts[SeedIds.Projects.Pay].ShouldBe(1);
    }

    private static Task<Guid> FeatureAsync(
        NpgsqlConnection connection, Guid owner, DateTimeOffset? verifiedAt, Guid? verifiedBy, string? agent, Guid? project = null) =>
        new WorkItemBuilder(project ?? SeedIds.Projects.Ship).WithType("feature").WithAssignee(owner)
            .WithStatusLine(Line, verifiedAt, verifiedBy, agent).InsertAsync(connection);

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
