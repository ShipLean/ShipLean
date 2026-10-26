using Dapper;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport;
using JiraWorld = ShipLean.WebApi.Tests.Import.Mapping.JiraWorld;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// Creating the new project: two jobs cannot aim at one key, and a run that died after creating the project carries on into it only when it is plainly the job's own.
[TestFixture]
public class ImportProjectKeyTests : ImportJobsFixture
{
    protected override IReadOnlyList<JiraIssue> Issues { get; } = [JiraWorld.Issue("PLAT-1")];

    [Test]
    public async Task Import_WhenAnEarlierRunLeftAnEmptyProjectOnlyTheActorAdministers_ShouldCarryOnIntoIt()
    {
        var job = await FailedJobAsync("ADP");
        var left = await NewProjectAsync("ADP");

        await PostJobAsync($"/api/import/jobs/{job.Id}/rerun", new { });
        var done = await WaitForAsync(job.Id, ImportJobState.Done);

        done.ProjectId.ShouldBe(left);
        (await QueryAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @left", new { left })).ShouldBe(1);
        (await QueryAsync<int>("SELECT count(*)::int FROM project WHERE key = 'ADP'")).ShouldBe(1);
    }

    [Test]
    public async Task Import_WhenTheProjectWithTheKeyHasAnotherAdmin_ShouldFailJobSayingSo()
    {
        var job = await FailedJobAsync("ADR");
        var taken = await NewProjectAsync("ADR");
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync(
                "INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @taken, @priya, 'admin')", new { id = Guid.NewGuid(), taken, priya = SeedIds.Users.Priya });
        }

        await PostJobAsync($"/api/import/jobs/{job.Id}/rerun", new { });

        var failed = await WaitForAsync(job.Id, j => j.Status == ImportJobState.Failed && j.Progress.Message.Contains("already taken", StringComparison.Ordinal));
        failed.ProjectId.ShouldBeNull();
        (await QueryAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @taken", new { taken })).ShouldBe(0);
    }

    // A job for a new project whose run failed before the project existed.
    private async Task<ImportJobDto> FailedJobAsync(string key)
    {
        var mapping = await SuggestAsync();
        Jira.Down = true;
        try
        {
            var job = await StartAsync(key, mapping);
            return await WaitForAsync(job.Id, ImportJobState.Failed);
        }
        finally
        {
            Jira.Down = false;
        }
    }
}
