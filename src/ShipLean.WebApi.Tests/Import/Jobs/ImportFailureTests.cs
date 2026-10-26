using Dapper;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport;
using JiraWorld = ShipLean.WebApi.Tests.Import.Mapping.JiraWorld;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// A run that cannot start ends as Failed with the reason in the job and the log, and leaves no half-made project.
[TestFixture]
public class ImportFailureTests : ImportJobsFixture
{
    protected override IReadOnlyList<JiraIssue> Issues { get; } = [JiraWorld.Issue("PLAT-1")];

    [Test]
    [Order(1)]
    public async Task Import_WhenJiraDown_ShouldFailJobWithMessage()
    {
        var mapping = await SuggestAsync();
        Jira.Down = true;
        var started = await StartAsync("FLA", mapping);

        var failed = await WaitForAsync(started.Id, ImportJobState.Failed);

        Jira.Down = false;
        failed.Progress.Message.ShouldContain("did not answer");
        (await LogAsync(started.Id)).ShouldContain(l => l.Level == ImportLogLevel.Error && l.Message.Contains("did not answer", StringComparison.Ordinal));
        (await QueryAsync<int>("SELECT count(*)::int FROM project WHERE key = 'FLA'")).ShouldBe(0);
        failed.FinishedAt.ShouldNotBeNull();
    }

    [Test]
    [Order(2)]
    public async Task Import_WhenJiraIsBackAndJobIsRerun_ShouldFinish()
    {
        var failed = (await GetAsync<IReadOnlyList<ImportJobDto>>("/api/import/jobs")).Single(j => j.Status == ImportJobState.Failed);

        await PostJobAsync($"/api/import/jobs/{failed.Id}/rerun", new { });

        var done = await WaitForAsync(failed.Id, ImportJobState.Done);
        done.ProjectId.ShouldNotBeNull();
    }

    [Test]
    [Order(3)]
    public async Task Import_WhenCredentialsRefused_ShouldFailJobWithMessage()
    {
        var wrong = Guid.NewGuid();
        await InsertConnectionAsync(wrong, await NewProjectAsync("OWA"));
        Connections.Arrange(wrong, JiraWorld.Credentials with { Token = "revoked" });
        var mapping = await SuggestAsync();
        var started = await StartAsync("FLB", mapping, connection: wrong, api: Api.ActingAs(ShipLean.TestSupport.Hosting.TestUser.Joel));

        var failed = await WaitForAsync(started.Id, ImportJobState.Failed);

        failed.Progress.Message.ShouldContain("Jira refused");
    }

    [Test]
    [Order(4)]
    public async Task Import_WhenConnectionWasReplaced_ShouldCarryOnWithTheCurrentOne()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var owner = await NewProjectAsync("OWB");
        await InsertConnectionAsync(first, owner);
        Connections.Arrange(first, JiraWorld.Credentials);
        var job = await StartAsync("FLC", await SuggestAsync(), connection: first);
        await WaitForAsync(job.Id, ImportJobState.Done);
        await ReplaceConnectionAsync(first, second, owner);
        Connections.Arrange(second, JiraWorld.Credentials);

        await PostJobAsync($"/api/import/jobs/{job.Id}/rerun", new { });
        await WaitForAsync(job.Id, j => j.Status == ImportJobState.Done && j.Progress.RerunResult is not null);

        (await QueryAsync<Guid>("SELECT connection_id FROM import_job WHERE id = @id", new { id = job.Id })).ShouldBe(second);
    }

    [Test]
    [Order(5)]
    public async Task Import_WhenConnectionWasRemoved_ShouldFailJobSayingSo()
    {
        var only = Guid.NewGuid();
        await InsertConnectionAsync(only, await NewProjectAsync("OWC"));
        Connections.Arrange(only, JiraWorld.Credentials);
        var job = await StartAsync("FLD", await SuggestAsync(), connection: only);
        await WaitForAsync(job.Id, ImportJobState.Done);
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE jira_connection SET archived_at = now() WHERE id = @only", new { only });
        }

        await PostJobAsync($"/api/import/jobs/{job.Id}/rerun", new { });

        var failed = await WaitForAsync(job.Id, ImportJobState.Failed);
        failed.Progress.Message.ShouldContain("no Jira connection");
    }

    private async Task InsertConnectionAsync(Guid id, Guid projectId)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync(
            "INSERT INTO jira_connection (id, project_id, base_url, email, created_by) VALUES (@id, @projectId, @url, @email, @joel)",
            new { id, projectId, url = JiraWorld.Credentials.BaseUrl, email = JiraWorld.Credentials.Email, joel = SeedIds.Users.Joel });
    }

    private async Task ReplaceConnectionAsync(Guid old, Guid replacement, Guid projectId)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE jira_connection SET archived_at = now() WHERE id = @old", new { old });
        await database.ExecuteAsync(
            "INSERT INTO jira_connection (id, project_id, base_url, email, created_by) VALUES (@replacement, @projectId, @url, @email, @joel)",
            new { replacement, projectId, url = JiraWorld.Credentials.BaseUrl, email = JiraWorld.Credentials.Email, joel = SeedIds.Users.Joel });
    }
}
