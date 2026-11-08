using System.Net;
using System.Text.Json.Nodes;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.ProjectSettings.Api;
using Shouldly;

namespace ShipLean.WebApi.Tests.IssueIntake.Settings;

// C5 over the real route and project access: members read the settings in the pinned shape
// (DtoFixtures/issueIntake/issue-intake-settings.json, which the client's type test reads too); only admins write.
[TestFixture]
public class IssueIntakeSettingsHttpTests : ApiFixture
{
    private static string PathOf(ArrangedProject project) => $"api/projects/{project.Id}/settings/issue-intake";

    private async Task<ArrangedProject> ArrangeAsync()
    {
        await using var database = await OpenDatabaseAsync();
        return await ArrangedProject.CreateAsync(database, (SeedIds.Users.Joel, ProjectRole.Admin), (SeedIds.Users.Priya, ProjectRole.Member));
    }

    [Test]
    public async Task Get_WhenMember_ShouldReturnContractShape()
    {
        var project = await ArrangeAsync();
        var pinned = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "issueIntake", "issue-intake-settings.json")));

        using var response = await Api.ActingAs(TestUser.Priya).GetAsync(PathOf(project));

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        JsonNode.DeepEquals(JsonNode.Parse(text), pinned).ShouldBeTrue(text);
    }

    [Test]
    public async Task Put_WhenMember_ShouldBeForbidden()
    {
        var project = await ArrangeAsync();
        var save = new { enabled = true, runAsUserId = (Guid?)null, proposalInstructions = "", maxRounds = 5 };

        using var refused = await Api.ActingAs(TestUser.Priya).PutJsonAsync(PathOf(project), save);
        using var saved = await Api.PutJsonAsync(PathOf(project), save);

        (await ApiClient.ReadApiErrorAsync(refused, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
        saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
    }
}
