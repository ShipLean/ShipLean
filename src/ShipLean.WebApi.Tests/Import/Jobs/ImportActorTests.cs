using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Import.Jobs;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using JiraWorld = ShipLean.WebApi.Tests.Import.Mapping.JiraWorld;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// A run acts as the person who last started it, and checks at the start of every run that they still may: an active account, and an admin of the project that owns the
// Jira connection (EMPT here) or of the project the import goes into.
[TestFixture]
public class ImportActorTests : ImportJobsFixture
{
    protected override IReadOnlyList<JiraIssue> Issues { get; } = [JiraWorld.Issue("PLAT-1")];

    [Test]
    [Order(1)]
    public async Task Import_WhenRerunByAnotherAdmin_ShouldRunAsThemEvenAfterTheCreatorIsDisabled()
    {
        await SetRoleAsync(SeedIds.Users.Priya, "admin");
        var mapping = await SuggestAsync();
        Jira.Down = true;
        var job = await StartAsync("ACA", mapping);
        await WaitForAsync(job.Id, ImportJobState.Failed);
        Jira.Down = false;
        await SetDisabledAsync(SeedIds.Users.Joel, true);
        var priya = Api.ActingAs(TestUser.Priya);
        try
        {
            using var response = await priya.PostJsonAsync($"/api/import/jobs/{job.Id}/rerun", Body(new { }));
            (await ReadAsync<ShipLean.Contracts.Import.ImportJobDto>(response)).Status.ShouldBe(ImportJobState.Queued);

            var done = await WaitForAsync(job.Id, j => j.Status == ImportJobState.Done, priya);

            (await QueryManyAsync<Guid>("SELECT user_id FROM project_member WHERE project_id = @id AND role = 'admin' AND archived_at IS NULL", new { id = done.ProjectId }))
                .ShouldBe([SeedIds.Users.Priya]);
            (await QueryAsync<Guid>("SELECT created_by FROM import_job WHERE id = @id", new { id = job.Id })).ShouldBe(SeedIds.Users.Priya);
        }
        finally
        {
            await SetDisabledAsync(SeedIds.Users.Joel, false);
        }
    }

    [Test]
    [Order(2)]
    public async Task Import_WhenTheActorIsDisabled_ShouldFailJobSayingSo()
    {
        var job = await StartAsync("ACB");
        await WaitForAsync(job.Id, ImportJobState.Done);
        await SetRoleAsync(SeedIds.Users.Tom, "admin");
        await QueueAsAsync(job.Id, SeedIds.Users.Tom, disable: true);

        var failed = await WaitForAsync(job.Id, ImportJobState.Failed);

        failed.Progress.Message.ShouldContain("can no longer run this import");
        failed.Progress.Message.ShouldContain("switched off");
        failed.Progress.Message.ShouldContain("Re-run");
    }

    [Test]
    [Order(3)]
    public async Task Import_WhenTheActorIsNoLongerAnAdmin_ShouldFailJobSayingSo()
    {
        var job = await StartAsync("ACC");
        await WaitForAsync(job.Id, ImportJobState.Done);
        await SetRoleAsync(SeedIds.Users.Priya, "member");
        await QueueAsAsync(job.Id, SeedIds.Users.Priya, disable: false);

        var failed = await WaitForAsync(job.Id, ImportJobState.Failed);

        failed.Progress.Message.ShouldContain("no longer an admin");
        (await QueryAsync<int>("SELECT count(*)::int FROM work_item w JOIN project p ON p.id = w.project_id WHERE p.key = 'ACC'")).ShouldBe(1);
    }

    // The person was valid when they queued the job; by the time the runner picks it up, they are not.
    private async Task QueueAsAsync(Guid jobId, Guid actor, bool disable)
    {
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE import_job SET status = 'queued', created_by = @actor, finished_at = NULL WHERE id = @jobId", new { jobId, actor });
        }

        if (disable)
        {
            await SetDisabledAsync(actor, true);
        }

        Host.Services.GetRequiredService<ImportQueue>().Enqueue(jobId);
    }

    private async Task SetRoleAsync(Guid user, string role)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync(
            """
            INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @project, @user, @role)
            ON CONFLICT (project_id, user_id) DO UPDATE SET role = @role, archived_at = NULL
            """,
            new { id = Guid.NewGuid(), project = SeedIds.Projects.Empt, user, role });
    }

    private async Task SetDisabledAsync(Guid user, bool disabled)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE app_user SET disabled_at = CASE WHEN @disabled THEN now() END WHERE id = @user", new { user, disabled });
    }
}
