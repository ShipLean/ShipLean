using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport.Hosting;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Comments;
using JiraWorld = ShipLean.WebApi.Tests.Import.Mapping.JiraWorld;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// The first host takes the runner out, so a started import stays queued. A second host on the same database is the process after a restart.
[TestFixture]
public class ImportRestartTests : ImportJobsFixture
{
    protected override IReadOnlyList<JiraIssue> Issues { get; } = [JiraWorld.Issue("PLAT-1"), JiraWorld.Issue("PLAT-2")];

    protected override bool RunnerEnabled => false;

    [Test]
    public async Task Import_WhenQueuedAtRestart_ShouldBePickedUpAgain()
    {
        var job = await StartAsync("RSA");
        (await GetJobAsync(job.Id)).Status.ShouldBe(ImportJobState.Queued);

        var done = await RestartAndWaitAsync(job.Id);

        done.Status.ShouldBe(ImportJobState.Done);
        (await QueryAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @id", new { id = done.ProjectId })).ShouldBe(2);
    }

    [Test]
    public async Task Import_WhenRunningAtRestart_ShouldBePickedUpAgain()
    {
        var job = await StartAsync("RSB");
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE import_job SET status = 'running' WHERE id = @id", new { id = job.Id });
        }

        var done = await RestartAndWaitAsync(job.Id);

        done.Status.ShouldBe(ImportJobState.Done);
        (await LogAsync(job.Id)).ShouldContain(l => l.Message.Contains("after a restart", StringComparison.Ordinal));
    }

    private async Task<ImportJobDto> RestartAndWaitAsync(Guid jobId)
    {
        await using var restarted = ShipLeanHost.Start(ConnectionString, TestUser.Joel, services =>
        {
            services.AddSingleton<IJiraClient>(Jira);
            services.AddSingleton<IJiraConnections>(Connections);
            services.AddSingleton<ICommentStore>(new Apply.RecordingCommentStore());
            services.AddSingleton<IAttachmentStore>(Attachments);
        });
        using var http = restarted.CreateClient();
        return await WaitForAsync(jobId, j => j.Status == ImportJobState.Done, new ShipLean.TestSupport.Fixtures.ApiClient(http));
    }
}
