using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.WebApi.Tests.Mcp.Planning.Reports;

namespace ShipLean.WebApi.Tests.InspectionMetrics;

// More cases of the real facts reader (K5). Each test takes its own task: picked up at 9 h, sent to review at 13 h on 5 Oct 2026,
// with one commit reported, every time set in SQL.
[TestFixture]
public class LeadTimeFactsReaderTests : ReportTestBase
{
    private const string Sha = "abc1234def";

    private static readonly DateTimeOffset T0 = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    [OneTimeSetUp]
    public async Task Arrange() => await SeedPlanAsync();

    // Mutation: read the agent of the task's last In-progress move.
    [Test]
    public async Task Read_WhenAgentStartedTask_ShouldReturnItsAgentName()
    {
        var task = await ArrangeTaskAsync("Claude Code");

        var facts = await ReadAsync(SeedIds.Projects.Ship, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5));

        facts.Tasks.Where(t => t.Key == task).Select(t => t.AgentName).ShouldBe(["Claude Code"]);
    }

    // Mutation: drop the project_id filter.
    [Test]
    public async Task Read_WhenTaskInOtherProject_ShouldNotReturnIt()
    {
        var task = await ArrangeTaskAsync(null);

        var facts = await ReadAsync(SeedIds.Projects.Part, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5));

        facts.Tasks.Select(t => t.Key).ShouldNotContain(task);
        facts.Moves.Select(m => m.ItemKey).ShouldNotContain(task);
    }

    // Mutation: drop archived_at IS NULL from the commit query.
    [Test]
    public async Task Read_WhenLinkArchived_ShouldNotReturnCommit()
    {
        var task = await ArrangeTaskAsync(null);
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "UPDATE work_item_link SET archived_at = @at FROM work_item w WHERE w.id = source_id AND w.key = @task AND link_type = 'url'", new { at = T0, task });

        var facts = await ReadAsync(SeedIds.Projects.Ship, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5));

        facts.Commits.Where(c => c.ItemKey == task).ShouldBeEmpty();
    }

    [Test]
    public async Task Read_WhenTaskEndedBeforeWindow_ShouldNotReturnIt()
    {
        var task = await ArrangeTaskAsync(null);

        var facts = await ReadAsync(SeedIds.Projects.Ship, new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 16));

        facts.Tasks.Select(t => t.Key).ShouldNotContain(task);
    }

    // The weekend grace: a task that ended one day after the window's last day is still selected, one that ended four days after is not.
    [Test]
    public async Task Read_WhenTaskEndedWithinTwoDaysAfterWindow_ShouldReturnIt()
    {
        var task = await ArrangeTaskAsync(null);

        (await ReadAsync(SeedIds.Projects.Ship, new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 3))).Tasks.Select(t => t.Key).ShouldContain(task);
        (await ReadAsync(SeedIds.Projects.Ship, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1))).Tasks.Select(t => t.Key).ShouldNotContain(task);
    }

    // The window's day is in the project's zone, so a task that ended at 23:00Z the day before fromDay is still returned.
    [Test]
    public async Task Read_WhenTaskEndedAt2300ZTheDayBeforeFromDay_ShouldReturnIt()
    {
        var task = await ArrangeTaskAsync(null, T0.AddHours(10));

        (await ReadAsync(SeedIds.Projects.Ship, new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 6))).Tasks.Select(t => t.Key).ShouldContain(task);
        (await ReadAsync(SeedIds.Projects.Ship, new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 7))).Tasks.Select(t => t.Key).ShouldNotContain(task);
    }

    // The Features tab counts all of a Feature's tasks, so a Feature with a task ending in the window brings its open tasks too.
    // Mutation: return only the windowed tasks (drop the Feature clause).
    [Test]
    public async Task Read_WhenFeatureHasTaskInWindowAndOpenTask_ShouldReturnBoth()
    {
        var ended = await ArrangeTaskAsync(null);
        var open = NextTask();

        var facts = await ReadAsync(SeedIds.Projects.Ship, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5));

        var both = facts.Tasks.Where(t => t.Key == ended || t.Key == open).ToList();
        both.Select(t => t.Key).ShouldBe([ended, open], ignoreOrder: true);
        both.Select(t => t.FeatureKey).Distinct().ShouldHaveSingleItem().ShouldNotBeNull();
    }

    // A commit of a readable repository is a work_item_commit row, its time the code_commit's authored_at; the task has no other
    // reason to be selected, so the commit alone decides.
    // Mutation: drop the work_item_commit branch of the union.
    [Test]
    public async Task Read_WhenCommitOfReadableRepository_ShouldReturnItWithItsAuthoredTime()
    {
        var task = NextTask();
        (await PickUpAsync(task)).IsToolError().ShouldBeFalse();
        const string sha = "0123456789abcdef0123456789abcdef01234567";
        var repository = Guid.NewGuid();
        await using var connection = await OpenDatabaseAsync();
        var id = await connection.QuerySingleAsync<Guid>("SELECT id FROM work_item WHERE key = @key", new { key = task });
        await connection.ExecuteAsync(
            "INSERT INTO code_repository (id, provider, host, path) VALUES (@repository, 'github', 'github.com', @path)", new { repository, path = $"acme/{repository}" });
        await connection.ExecuteAsync(
            "INSERT INTO work_item_commit (id, work_item_id, repository_id, sha, position, added_via) VALUES (gen_random_uuid(), @id, @repository, @sha, 1, 'report_task')",
            new { id, repository, sha });
        await connection.ExecuteAsync(
            "INSERT INTO code_commit (repository_id, sha, parent_shas, subject, author_name, authored_at, additions, deletions, files_changed, paths, read_at) VALUES (@repository, @sha, '{}', 'Work', 'Ann', @at, 1, 0, 1, '{}', @at)",
            new { repository, sha, at = T0.AddHours(5) });

        var facts = await ReadAsync(SeedIds.Projects.Ship, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5));

        facts.Commits.Where(c => c.ItemKey == task).Select(c => (c.Sha7, c.CommittedAt)).ShouldBe([(sha[..7], (DateTimeOffset?)T0.AddHours(5))]);
    }

    private async Task<string> ArrangeTaskAsync(string? agent, DateTimeOffset? pickedUpAt = null)
    {
        var t0 = pickedUpAt ?? T0;
        var task = NextTask();
        (await PickUpAsync(task)).IsToolError().ShouldBeFalse();
        await using var connection = await OpenDatabaseAsync();
        var id = await connection.QuerySingleAsync<Guid>("SELECT id FROM work_item WHERE key = @key", new { key = task });
        await connection.ExecuteAsync("UPDATE revision SET changed_at = @at WHERE work_item_id = @id AND rev = 1", new { id, at = t0.AddHours(-1) });
        await connection.ExecuteAsync(
            "UPDATE revision SET changed_at = @at, agent_name = @agent WHERE work_item_id = @id AND rev > 1", new { id, at = t0, agent });
        var rev = await connection.QuerySingleAsync<int>("SELECT max(rev) + 1 FROM revision WHERE work_item_id = @id", new { id });
        await new RevisionBuilder(id).WithRev(rev).WithChangedAt(t0.AddHours(4)).WithChangedBy(SeedIds.Users.Joel)
            .WithChanges($$"""[{"field":"state","from":null,"to":"{{SeedIds.States.Ship.TaskInReview}}"}]""").InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @state, revision = @rev WHERE id = @id", new { id, state = SeedIds.States.Ship.TaskInReview, rev });
        var reported = await ReportAsync($$"""{"item":"{{task}}","status":"partial","commits":[{"sha":"{{Sha}}","subject":"Work","url":"https://git.example.test/acme/ship/commit/{{Sha}}"}]}""");
        reported.IsToolError().ShouldBeFalse(reported.ResultText());
        await connection.ExecuteAsync(
            "UPDATE work_item_link SET committed_at = @at WHERE source_id = @id AND link_type = 'url'", new { id, at = t0.AddHours(3) });
        return task;
    }

    private async Task<LeadTimeFacts> ReadAsync(Guid projectId, DateOnly from, DateOnly to)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ILeadTimeFactsReader>().ReadAsync(projectId, from, to, CancellationToken.None);
    }
}
