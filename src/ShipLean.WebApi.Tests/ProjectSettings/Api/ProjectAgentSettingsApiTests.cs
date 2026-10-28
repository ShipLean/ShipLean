using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.ProjectSettings;
using ShipLean.WebApi.Tests.Instance.AgentConnections;

namespace ShipLean.WebApi.Tests.ProjectSettings.Api;

// One seeded scenario: the agent connection, repository and base branch on a project's General settings. Every test arranges a
// project of its own, with Joel (the signed-in user, an instance admin) as its admin and Priya as a member.
[TestFixture]
public class ProjectAgentSettingsApiTests : ApiFixture
{
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private async Task<ArrangedProject> ArrangeAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await ArrangedProject.CreateAsync(connection, (SeedIds.Users.Joel, ProjectRole.Admin), (SeedIds.Users.Priya, ProjectRole.Member));
    }

    private async Task<ProjectSettingsDto> LoadAsync(ArrangedProject project, ApiClient? api = null)
    {
        using var response = await (api ?? Api).GetAsync($"api/projects/{project.Id}/settings");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<ProjectSettingsDto>(text, WireOptions)!;
    }

    private static ProjectSettingsRequest Request(ProjectSettingsDto loaded, Guid? connectionId, string? repoUrl, string? branch)
        => new(
            new ProjectGeneralRequest(loaded.General.Key, loaded.General.Name, loaded.General.Description, loaded.General.WorkingDays, connectionId, repoUrl, branch),
            [.. loaded.Members.Select(m => new MemberPut(m.Email, m.Role))],
            loaded.Sprints,
            new StateChanges([], [], [], []));

    private Task<HttpResponseMessage> PutAsync(ArrangedProject project, ProjectSettingsRequest request, ApiClient? api = null)
        => (api ?? Api).PutJsonAsync($"api/projects/{project.Id}/settings", JsonSerializer.SerializeToElement(request, WireOptions));

    private async Task<Guid> NewConnectionAsync(string name) => (await AgentConnectionsWire.CreateAsync(Api, name)).Connection.Id;

    private async Task<IProjectAgentSettings> ReadAsync(Func<IProjectAgentSettings, Task> check)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<IProjectAgentSettings>();
        await check(settings);
        return settings;
    }

    [Test]
    public async Task Settings_WhenNothingChosen_ShouldReadNoConnectionAndMainBranch()
    {
        var project = await ArrangeAsync();

        var loaded = await LoadAsync(project);

        loaded.General.AgentConnectionId.ShouldBeNull();
        loaded.General.RepoUrl.ShouldBeNull();
        loaded.General.RepoBaseBranch.ShouldBeNull();
        await ReadAsync(async s => (await s.GetAsync(project.Id)).ShouldBe(new ProjectAgentSettings(null, null, "main")));
    }

    [Test]
    public async Task Settings_WhenAgentFieldsSaved_ShouldReadBackAndReachTheContract()
    {
        var project = await ArrangeAsync();
        var connectionId = await NewConnectionAsync("Picked relay");
        var loaded = await LoadAsync(project);

        using var response = await PutAsync(project, Request(loaded, connectionId, "  https://github.com/ShipLean/ShipLean.git ", " release/1.2 "));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var reloaded = await LoadAsync(project);
        reloaded.General.AgentConnectionId.ShouldBe(connectionId);
        reloaded.General.RepoUrl.ShouldBe("https://github.com/ShipLean/ShipLean.git");
        reloaded.General.RepoBaseBranch.ShouldBe("release/1.2");
        reloaded.General.AgentConnections.ShouldContain(o => o.Id == connectionId && o.Name == "Picked relay" && !o.Archived);
        await ReadAsync(async s => (await s.GetAsync(project.Id)).ShouldBe(new ProjectAgentSettings(connectionId, "https://github.com/ShipLean/ShipLean.git", "release/1.2")));
    }

    [Test]
    public async Task Settings_WhenAgentFieldsBlank_ShouldClearThemAndDefaultTheBranch()
    {
        var project = await ArrangeAsync();
        var connectionId = await NewConnectionAsync("Cleared relay");
        using var first = await PutAsync(project, Request(await LoadAsync(project), connectionId, "git@github.com:ShipLean/ShipLean.git", "dev"));
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var cleared = await PutAsync(project, Request(await LoadAsync(project), null, "  ", ""));

        cleared.StatusCode.ShouldBe(HttpStatusCode.OK);
        var reloaded = await LoadAsync(project);
        reloaded.General.AgentConnectionId.ShouldBeNull();
        reloaded.General.RepoUrl.ShouldBeNull();
        await ReadAsync(async s => (await s.GetAsync(project.Id)).ShouldBe(new ProjectAgentSettings(null, null, "main")));
    }

    [Test]
    public async Task Settings_WhenConnectionArchived_ShouldNotBePickableButKeepTheCurrentChoice()
    {
        var project = await ArrangeAsync();
        var connectionId = await NewConnectionAsync("Soon archived");
        var other = await NewConnectionAsync("Never archived");
        using var picked = await PutAsync(project, Request(await LoadAsync(project), connectionId, null, null));
        picked.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var archive = await Api.PostJsonAsync($"{AgentConnectionsWire.Route}/{connectionId}/archive");
        archive.StatusCode.ShouldBe(HttpStatusCode.OK);

        var loaded = await LoadAsync(project);
        loaded.General.AgentConnectionId.ShouldBe(connectionId);
        loaded.General.AgentConnections.ShouldContain(o => o.Id == connectionId && o.Archived);
        loaded.General.AgentConnections.ShouldContain(o => o.Id == other && !o.Archived);
        using var kept = await PutAsync(project, Request(loaded, connectionId, "https://github.com/ShipLean/ShipLean", null));
        kept.StatusCode.ShouldBe(HttpStatusCode.OK, await kept.Content.ReadAsStringAsync());

        var second = await ArrangeAsync();
        using var refused = await PutAsync(second, Request(await LoadAsync(second), connectionId, null, null));
        (await ApiClient.ReadApiErrorAsync(refused, HttpStatusCode.BadRequest)).Field.ShouldBe("agentConnectionId");
        (await LoadAsync(second)).General.AgentConnections.ShouldNotContain(o => o.Id == connectionId);
    }

    [Test]
    public async Task Settings_WhenConnectionUnknown_ShouldBeValidationOnThatField()
    {
        var project = await ArrangeAsync();

        using var response = await PutAsync(project, Request(await LoadAsync(project), Guid.NewGuid(), null, null));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Field.ShouldBe("agentConnectionId");
    }

    [TestCase("not a url")]
    [TestCase("http://github.com/ShipLean/ShipLean")]
    [TestCase("https://user:secret@github.com/ShipLean/ShipLean")]
    [TestCase("https://github.com/ShipLean/ShipLean?x=1")]
    public async Task Settings_WhenRepoUrlInvalid_ShouldBeValidationOnThatField(string url)
    {
        var project = await ArrangeAsync();
        var loaded = await LoadAsync(project);

        using var response = await PutAsync(project, Request(loaded, null, url, null));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Field.ShouldBe("repoUrl");
        error.Error.ShouldNotContain("secret");
    }

    [TestCase("-bad")]
    [TestCase("a..b")]
    [TestCase("has space")]
    [TestCase("ends/")]
    [TestCase("topic.lock")]
    public async Task Settings_WhenBaseBranchInvalid_ShouldBeValidationOnThatField(string branch)
    {
        var project = await ArrangeAsync();

        using var response = await PutAsync(project, Request(await LoadAsync(project), null, null, branch));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Field.ShouldBe("repoBaseBranch");
    }

    [Test]
    public async Task Settings_WhenMemberTriesToSetAgentFields_ShouldBeForbiddenAndChangeNothing()
    {
        var project = await ArrangeAsync();
        var connectionId = await NewConnectionAsync("Admin only");
        var loaded = await LoadAsync(project);
        var priya = Api.ActingAs(TestUser.Priya);

        using var response = await PutAsync(project, Request(loaded, connectionId, "https://github.com/ShipLean/ShipLean", "main"), priya);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
        (await LoadAsync(project)).General.AgentConnectionId.ShouldBeNull();
    }

    [Test]
    public async Task Settings_WhenMemberReads_ShouldSeeOptionNamesOnly()
    {
        var project = await ArrangeAsync();
        await NewConnectionAsync("Named option");

        var asMember = await LoadAsync(project, Api.ActingAs(TestUser.Priya));

        var wire = JsonSerializer.Serialize(asMember, JsonSerializerOptions.Web);
        wire.ShouldContain("Named option");
        wire.ShouldNotContain("relay.example.com");
        wire.ShouldNotContain("Last4");
    }
}
