using System.Net;
using Dapper;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport;
using JiraWorld = ShipLean.WebApi.Tests.Import.Mapping.JiraWorld;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// With the runner out, a started job stays queued, so a second job aiming at its new project key is refused at once.
[TestFixture]
public class ImportQueuedKeyTests : ImportJobsFixture
{
    protected override IReadOnlyList<JiraIssue> Issues { get; } = [JiraWorld.Issue("PLAT-1")];

    protected override bool RunnerEnabled => false;

    [Test]
    public async Task Import_WhenAnotherJobAimsAtTheSameNewKey_ShouldRefuseToStart()
    {
        var other = Guid.NewGuid();
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync(
                "INSERT INTO jira_connection (id, project_id, base_url, email, created_by) VALUES (@other, @owner, @url, @email, @joel)",
                new { other, owner = await NewProjectAsync("KEO"), url = JiraWorld.Credentials.BaseUrl, email = JiraWorld.Credentials.Email, joel = SeedIds.Users.Joel });
        }

        var mapping = await SuggestAsync();
        var first = await StartAsync("KEY", mapping);
        var request = new ImportStartRequest(other, JiraWorld.ProjectKey, new ImportTarget(ImportTargetKind.NewProject, "KEY", "Project KEY", null), mapping);

        using var second = await Api.PostJsonAsync("/api/import/jobs", Body(request));

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await second.Content.ReadAsStringAsync()).ShouldContain("creating KEY");
        (await GetJobAsync(first.Id)).Status.ShouldBe(ImportJobState.Queued);
    }
}
