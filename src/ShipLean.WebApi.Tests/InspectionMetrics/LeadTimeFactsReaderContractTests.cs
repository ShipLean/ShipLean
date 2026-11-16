using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Planning.Reports;

namespace ShipLean.WebApi.Tests.InspectionMetrics;

// What the real facts reader (B1) must return (K5), on one task's history written through the MCP tools: picked up, two commits
// reported, Blocked and unblocked, sent to review and reviewed twice. The tools stamp the host's clock, so the arrange step then
// sets every time in SQL, and every assertion is an exact instant. Red until B1 replaces the stub reader: each test fails because
// the stub returns no facts. One scenario per class.
[TestFixture]
public class LeadTimeFactsReaderContractTests : ReportTestBase
{
    private const string FirstSha = "abc1234def";
    private const string SecondSha = "bcd2345efa";
    private const string CommitUrl = "https://git.example.test/acme/ship/commit/";

    private static readonly Guid Ship = SeedIds.Projects.Ship;
    private static readonly DateTimeOffset T0 = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    private string _task = string.Empty;
    private string _story = string.Empty;
    private string _feature = string.Empty;
    private string _title = string.Empty;
    private string _state = string.Empty;
    private LeadTimeFacts _facts = LeadTimeFacts.Empty;
    private LeadTimeFacts _laterWindow = LeadTimeFacts.Empty;

    [OneTimeSetUp]
    public async Task Arrange()
    {
        await SeedPlanAsync();
        _task = NextTask();
        (await PickUpAsync(_task)).IsToolError().ShouldBeFalse();
        await using var connection = await OpenDatabaseAsync();
        var id = await connection.QuerySingleAsync<Guid>("SELECT id FROM work_item WHERE key = @key", new { key = _task });
        (_story, _feature) = await connection.QuerySingleAsync<(string, string)>(
            "SELECT story.key, feature.key FROM work_item task JOIN work_item story ON story.id = task.parent_id JOIN work_item feature ON feature.id = story.parent_id WHERE task.id = @id",
            new { id });
        _title = await connection.QuerySingleAsync<string>("SELECT title FROM work_item WHERE id = @id", new { id });
        // Revision 1 is the creation, a real move to New (Proposed) that the reader returns. pick_up's revision is the task's first
        // In-progress move and is made by an agent; the later back-to-work move is made by another, so the first one must win.
        await connection.ExecuteAsync("UPDATE revision SET changed_at = @at, changed_by = @by WHERE work_item_id = @id AND rev = 1", new { id, at = T0, by = SeedIds.Users.Joel });
        await connection.ExecuteAsync("UPDATE revision SET changed_at = @at, agent_name = 'Claude Code' WHERE work_item_id = @id AND rev > 1", new { id, at = T0.AddHours(1) });

        // Two commits, the later one the last. committed_at is set in SQL here only: the tool that sends it comes in a later wave.
        var reported = await ReportAsync($$"""
            {"item":"{{_task}}","status":"partial","commits":[
              {"sha":"{{FirstSha}}","subject":"First pass","url":"{{CommitUrl}}{{FirstSha}}"},
              {"sha":"{{SecondSha}}","subject":"Fix","url":"{{CommitUrl}}{{SecondSha}}"}]}
            """);
        reported.IsToolError().ShouldBeFalse(reported.ResultText());
        await SetCommitTimeAsync(connection, id, FirstSha, T0.AddHours(3));
        await SetCommitTimeAsync(connection, id, SecondSha, T0.AddHours(31));

        // Blocked from 2 h to 3 h, then to review, back to work and to review again.
        await TagChangeAsync(connection, id, T0.AddHours(2), "[]", """["blocked"]""");
        await TagChangeAsync(connection, id, T0.AddHours(3), """["blocked"]""", "[]");
        await MoveAsync(connection, id, T0.AddHours(4), SeedIds.States.Ship.TaskInReview);
        await MoveAsync(connection, id, T0.AddHours(5), SeedIds.States.Ship.TaskActive, "Cursor");
        await MoveAsync(connection, id, T0.AddHours(7), SeedIds.States.Ship.TaskInReview);

        _state = await StateOfAsync(_task);
        var writer = await TokenAsync(TestUser.Joel, ReadWrite);
        (await CallToolAsync(writer, "log_review", $$"""{"item":"{{_task}}","verdict":"changes_requested","note":"Missing a test."}""")).IsToolError().ShouldBeFalse();
        (await CallToolAsync(writer, "log_review", $$"""{"item":"{{_task}}","verdict":"approved"}""")).IsToolError().ShouldBeFalse();
        await connection.ExecuteAsync(
            "UPDATE review_event SET at = CASE verdict WHEN 'changesRequested' THEN @changes ELSE @approved END WHERE work_item_id = @id",
            new { id, changes = T0.AddHours(4.5), approved = T0.AddHours(7.5) });

        // The whole span of the task, and a window that starts after the task started but holds its last commit.
        _facts = await ReadAsync(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 9));
        _laterWindow = await ReadAsync(new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 6));
    }

    // Mutation: read the assignee from the creator instead of assigned_to.
    [Test]
    public void Read_WhenTaskPickedUp_ShouldReturnTheTaskWithItsParentFeatureAndAssigneeTurnedGreenByB1()
    {
        var task = _facts.Tasks.SingleOrDefault(t => t.Key == _task);

        (task?.ParentKey, task?.ParentType, task?.FeatureKey, task?.AssigneeId, task?.Title, task?.StateName)
            .ShouldBe((_story, InspectionParentType.Story, _feature, SeedIds.Users.Joel, _title, _state));
        _facts.People.ContainsKey(SeedIds.Users.Joel).ShouldBeTrue();
        task?.AssigneeName.ShouldNotBeNullOrWhiteSpace();
        task?.AssigneeName.ShouldBe(_facts.People[SeedIds.Users.Joel]);
    }

    // Mutation: take the agent from the task's last In-progress move (Cursor) instead of its first (Claude Code).
    [Test]
    public void Read_WhenTaskStartedByAnAgent_ShouldReturnTheAgentOfTheFirstInProgressMoveTurnedGreenByB1()
    {
        _facts.Tasks.SingleOrDefault(t => t.Key == _task)?.AgentName.ShouldBe("Claude Code");
    }

    // Mutation: take the revision's creation time as the move's time.
    [Test]
    public void Read_WhenTaskPickedUpAndSentToReview_ShouldReturnEveryMoveWithItsTimeAndCategoryTurnedGreenByB1()
    {
        var moves = _facts.Moves.Where(m => m.ItemKey == _task).OrderBy(m => m.At).Select(m => (m.At, m.Category, m.UserId, m.AgentName)).ToList();

        moves.ShouldBe(
        [
            (T0, StateCategory.Proposed, (Guid?)SeedIds.Users.Joel, (string?)null),
            (T0.AddHours(1), StateCategory.InProgress, SeedIds.Users.Joel, "Claude Code"),
            (T0.AddHours(4), StateCategory.Resolved, SeedIds.Users.Joel, null),
            (T0.AddHours(5), StateCategory.InProgress, SeedIds.Users.Joel, "Cursor"),
            (T0.AddHours(7), StateCategory.Resolved, SeedIds.Users.Joel, null),
        ]);
    }

    // Mutation: return the commit's link creation time instead of committed_at.
    [Test]
    public void Read_WhenCommitsReported_ShouldReturnEachShaWithItsCommitTimeTurnedGreenByB1()
    {
        var commits = _facts.Commits.Where(c => c.ItemKey == _task).OrderBy(c => c.CommittedAt).Select(c => (c.Sha7, c.CommittedAt)).ToList();

        commits.ShouldBe([(FirstSha[..7], (DateTimeOffset?)T0.AddHours(3)), (SecondSha[..7], T0.AddHours(31))]);
    }

    // Mutation: read only the tag change that added Blocked.
    [Test]
    public void Read_WhenTaskBlockedThenUnblocked_ShouldReturnBothTagChangesTurnedGreenByB1()
    {
        var tags = _facts.BlockedTags.Where(t => t.ItemKey == _task).OrderBy(t => t.At).Select(t => (t.At, t.BlockedAfter)).ToList();

        tags.ShouldBe([(T0.AddHours(2), true), (T0.AddHours(3), false)]);
    }

    // Mutation: read only the approving review.
    [Test]
    public void Read_WhenChangesRequestedThenApproved_ShouldReturnBothReviewsInOrderTurnedGreenByB1()
    {
        var reviews = _facts.Reviews.Where(r => r.ItemKey == _task).OrderBy(r => r.At).Select(r => (r.At, r.Verdict)).ToList();

        reviews.ShouldBe([(T0.AddHours(4.5), ReviewVerdict.ChangesRequested), (T0.AddHours(7.5), ReviewVerdict.Approved)]);
    }

    // Mutation: keep only the revisions and commits inside the window's days.
    [Test]
    public void Read_WhenWindowStartsAfterTheTaskStarted_ShouldReturnItsWholeHistoryTurnedGreenByB1()
    {
        _laterWindow.Tasks.Select(t => t.Key).ShouldContain(_task);
        _laterWindow.Moves.Where(m => m.ItemKey == _task).Select(m => m.Category).ShouldBe(
            [StateCategory.Proposed, StateCategory.InProgress, StateCategory.Resolved, StateCategory.InProgress, StateCategory.Resolved], ignoreOrder: true);
        _laterWindow.Commits.Count(c => c.ItemKey == _task).ShouldBe(2);
        _laterWindow.Reviews.Count(r => r.ItemKey == _task).ShouldBe(2);
        _laterWindow.BlockedTags.Count(t => t.ItemKey == _task).ShouldBe(2);
    }

    private async Task<LeadTimeFacts> ReadAsync(DateOnly from, DateOnly to)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ILeadTimeFactsReader>().ReadAsync(Ship, from, to, CancellationToken.None);
    }

    private static async Task SetCommitTimeAsync(Npgsql.NpgsqlConnection connection, Guid id, string sha, DateTimeOffset at)
    {
        var updated = await connection.ExecuteAsync(
            "UPDATE work_item_link SET committed_at = @at WHERE source_id = @id AND link_type = 'url' AND title LIKE @title",
            new { id, at, title = $"commit {sha[..7]} %" });
        updated.ShouldBe(1);
    }

    private static async Task MoveAsync(Npgsql.NpgsqlConnection connection, Guid id, DateTimeOffset at, Guid state, string? agent = null)
    {
        var rev = await NextRevAsync(connection, id);
        var builder = new RevisionBuilder(id).WithRev(rev).WithChangedAt(at).WithChangedBy(SeedIds.Users.Joel);
        if (agent is not null)
        {
            builder.WithAgent(agent);
        }
        await builder
            .WithChanges($$"""[{"field":"state","from":null,"to":"{{state}}"}]""").InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @state, revision = @rev WHERE id = @id", new { id, state, rev });
    }

    private static async Task TagChangeAsync(Npgsql.NpgsqlConnection connection, Guid id, DateTimeOffset at, string from, string to)
    {
        var rev = await NextRevAsync(connection, id);
        await new RevisionBuilder(id).WithRev(rev).WithChangedAt(at).WithChangedBy(SeedIds.Users.Joel)
            .WithChanges($$"""[{"field":"tags","from":{{from}},"to":{{to}}}]""").InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE work_item SET revision = @rev WHERE id = @id", new { id, rev });
    }

    private static async Task<int> NextRevAsync(Npgsql.NpgsqlConnection connection, Guid id)
        => await connection.QuerySingleAsync<int>("SELECT max(rev) + 1 FROM revision WHERE work_item_id = @id", new { id });
}
