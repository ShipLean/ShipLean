using System.Net;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// Imports belong to project admins. The seeded MOB import and connection are Joel's to administer; Priya and Tom are MOB members, Kai is in no project.
[TestFixture]
public class ImportAccessTests : ApiFixture
{
    private static readonly string Job = $"/api/import/jobs/{SeedIds.ImportJobs.MobImport}";

    private static IEnumerable<TestCaseData> JobRoutes()
    {
        yield return new TestCaseData("GET", Job, null);
        yield return new TestCaseData("POST", $"{Job}/stop", "{}");
        yield return new TestCaseData("POST", $"{Job}/retry", "{}");
        yield return new TestCaseData("POST", $"{Job}/skip", "{}");
        yield return new TestCaseData("POST", $"{Job}/rerun", "{}");
        yield return new TestCaseData("GET", $"{Job}/log", null);
        yield return new TestCaseData("GET", $"{Job}/errors", null);
        yield return new TestCaseData("POST", "/api/import/jobs", $$$"""{"connectionId":"{{{SeedIds.JiraConnections.Mob}}}","jiraProjectKey":"X","target":{"kind":"newProject","key":"ACC","name":"Acc","projectId":null},"mapping":{"issueTypes":[],"statuses":[],"users":[],"fields":{"storyPointsFieldId":"","candidates":[],"includeComponents":true,"includeComments":true,"includeAttachments":true,"includeChangelog":true},"totals":{"issues":0,"comments":0,"attachments":0,"revisions":0,"sprints":0,"people":0},"warnings":[]}}""");
        yield return new TestCaseData("POST", "/api/import/mapping", $$$"""{"connectionId":"{{{SeedIds.JiraConnections.Mob}}}","jiraProjectKey":"X","previousJobId":null}""");
        yield return new TestCaseData("GET", $"/api/projects/{SeedIds.Projects.Mob}/import/jobs", null);
    }

    [TestCaseSource(nameof(JobRoutes))]
    public async Task Import_WhenMemberButNotAdmin_ShouldBeForbidden(string method, string url, string? body)
    {
        using var response = await SendAsync(Api.ActingAs(TestUser.Priya), method, url, body);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
    }

    [TestCaseSource(nameof(JobRoutes))]
    public async Task Import_WhenNotAMember_ShouldBeNotFound(string method, string url, string? body)
    {
        using var response = await SendAsync(Api.ActingAs(TestUser.Kai), method, url, body);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Import_WhenListedByAnAdmin_ShouldShowOnlyJobsOfProjectsTheyAdminister()
    {
        var mine = await Api.ActingAs(TestUser.Joel).GetAsync("/api/import/jobs");
        var priyas = await Api.ActingAs(TestUser.Priya).GetAsync("/api/import/jobs");

        (await mine.Content.ReadAsStringAsync()).ShouldContain(SeedIds.ImportJobs.MobImport.ToString());
        (await priyas.Content.ReadAsStringAsync()).ShouldNotContain(SeedIds.ImportJobs.MobImport.ToString());
    }

    private static Task<HttpResponseMessage> SendAsync(ApiClient api, string method, string url, string? body)
        => method == "GET" ? api.GetAsync(url) : api.PostJsonAsync(url, System.Text.Json.Nodes.JsonNode.Parse(body!));
}
