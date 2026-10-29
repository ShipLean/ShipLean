using ShipLean.Contracts.Events;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport;
using JiraWorld = ShipLean.WebApi.Tests.Import.Mapping.JiraWorld;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// One import of a small Jira project into a new project, then a re-run of it. The child PLAT-5 comes before its epic PLAT-20 in key order, and the sprints hand over on one day.
[TestFixture]
public class ImportRunTests : ImportJobsFixture
{
    private static readonly JiraSprint Sprint1 = new(1, "Sprint 1", "closed", JiraWorld.Day, JiraWorld.Day.AddDays(14), "First");
    private static readonly JiraSprint Sprint2 = new(2, "Sprint 2", "active", JiraWorld.Day.AddDays(14), JiraWorld.Day.AddDays(28), null);

    private static Guid _jobId;
    private static Guid _projectId;

    protected override IReadOnlyList<JiraIssue> Issues { get; } =
    [
        JiraWorld.Issue("PLAT-5", parent: "PLAT-20"),
        JiraWorld.Issue("PLAT-10", "Epic"),
        JiraWorld.Issue("PLAT-11", parent: "PLAT-10", points: 5, sprints: [Sprint1]),
        JiraWorld.Issue("PLAT-12", sprints: [Sprint2]),
        JiraWorld.Issue("PLAT-20", "Epic"),
    ];

    [Test]
    [Order(1)]
    public async Task Import_WhenRun_ShouldKeepJiraNumbersAndAdvanceSequence()
    {
        var started = await StartAsync("RUNA");
        started.Status.ShouldBe(ImportJobState.Queued);
        _jobId = started.Id;

        var done = await WaitForAsync(_jobId, ImportJobState.Done);

        _projectId = done.ProjectId!.Value;
        done.Progress.Percent.ShouldBe(100);
        done.Progress.Counts.Issues.ShouldBe(new ImportEntityCount(5, 5, 0));
        (await QueryManyAsync<string>("SELECT key FROM work_item WHERE project_id = @_projectId ORDER BY number", new { _projectId })).ShouldBe(["RUNA-5", "RUNA-10", "RUNA-11", "RUNA-12", "RUNA-20"]);
        (await QueryAsync<int>("SELECT next_item_number FROM project WHERE id = @_projectId", new { _projectId })).ShouldBe(21);
        (await QueryAsync<string>("SELECT role FROM project_member WHERE project_id = @_projectId AND user_id = @joel", new { _projectId, joel = SeedIds.Users.Joel })).ShouldBe("admin");
    }

    [Test]
    [Order(2)]
    public async Task Import_WhenParentComesAfterChild_ShouldStillLinkThem()
    {
        var linked = await QueryAsync<bool>(
            "SELECT c.parent_id = p.id FROM work_item c JOIN work_item p ON p.project_id = c.project_id AND p.number = 20 WHERE c.project_id = @_projectId AND c.number = 5", new { _projectId });

        linked.ShouldBeTrue();
    }

    [Test]
    [Order(3)]
    public async Task Import_WhenNewProject_ShouldHaveNoDefaultFirstSprint()
    {
        var sprints = await QueryManyAsync<(string Name, DateTime Start)>("SELECT name AS Name, start_date::timestamp AS Start FROM sprint WHERE project_id = @_projectId ORDER BY start_date", new { _projectId });

        sprints.Select(s => s.Name).ShouldBe(["Sprint 1", "Sprint 2"]);
        sprints[0].Start.Date.ShouldBe(JiraWorld.Day.UtcDateTime.Date);
    }

    [Test]
    [Order(3)]
    public async Task Import_WhenNewProject_ShouldNameSprintsByNumber()
    {
        (await QueryAsync<string>("SELECT sprint_naming FROM project WHERE id = @_projectId", new { _projectId })).ShouldBe("number");
    }

    [Test]
    [Order(4)]
    public async Task Import_WhenSprintsHandOverOnOneDay_ShouldLogTheShiftAsAWarning()
    {
        var job = await StartAsync("RUNB");
        await WaitForAsync(job.Id, ImportJobState.Done);

        var log = await LogAsync(job.Id);

        log.ShouldContain(l => l.Level == ImportLogLevel.Warning && l.Message.Contains("Sprint 2", StringComparison.Ordinal));
        log.ShouldContain(l => l.Level == ImportLogLevel.Info && l.Message == "Import finished");
        log.Select(l => l.Id).ShouldBe(log.Select(l => l.Id).Order());
    }

    [Test]
    [Order(5)]
    public void Import_WhenRunning_ShouldPublishProgressWithoutAClientId()
    {
        var progress = Events.OfType<ImportProgress>().Where(e => e.JobId == _jobId).ToList();

        progress.ShouldContain(e => e.State == ImportJobState.Running && e.Percent > 0);
        progress.ShouldContain(e => e.State == ImportJobState.Done && e.Percent == 100);
        progress.ShouldAllBe(e => e.ProjectId == _projectId);
        Events.Where(e => e.ProjectId == _projectId).ShouldAllBe(e => e.ClientId == null);
    }

    [Test]
    [Order(6)]
    public async Task Import_WhenRerun_ShouldUpdateNotDuplicate()
    {
        var before = await QueryAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @_projectId", new { _projectId });

        var queued = await PostJobAsync($"/api/import/jobs/{_jobId}/rerun", new { });
        queued.Progress.Rerun.ShouldBeTrue();
        var done = await WaitForAsync(_jobId, j => j.Status == ImportJobState.Done && j.Progress.RerunResult is not null);

        (await QueryAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @_projectId", new { _projectId })).ShouldBe(before);
        done.Progress.RerunResult.ShouldBe(new ImportRerunResult(5, 0, 0));
        (await QueryAsync<int>("SELECT next_item_number FROM project WHERE id = @_projectId", new { _projectId })).ShouldBe(21);
        (await QueryAsync<int>("SELECT count(*)::int FROM sprint WHERE project_id = @_projectId", new { _projectId })).ShouldBe(2);
        (await QueryAsync<bool>("SELECT c.parent_id = p.id FROM work_item c JOIN work_item p ON p.project_id = c.project_id AND p.number = 20 WHERE c.project_id = @_projectId AND c.number = 5", new { _projectId })).ShouldBeTrue();
    }

    [Test]
    [Order(7)]
    public async Task Import_WhenRerun_ShouldKeepTheEarlierMapping()
    {
        var mapping = await SuggestAsync(_jobId);

        mapping.IssueTypes.ShouldAllBe(t => !t.IsNewSinceLastRun);
        mapping.Statuses.ShouldAllBe(s => !s.IsNewSinceLastRun);
    }
}
