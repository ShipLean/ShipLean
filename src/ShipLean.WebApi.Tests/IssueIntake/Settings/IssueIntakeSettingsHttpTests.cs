using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Projects;
using ShipLean.WebApi.Features.ProjectSettings;
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
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private static string PathOf(ArrangedProject project) => $"api/projects/{project.Id}/settings/issue-intake";

    private async Task<ArrangedProject> ArrangeAsync()
    {
        await using var database = await OpenDatabaseAsync();
        return await ArrangedProject.CreateAsync(database, (SeedIds.Users.Joel, ProjectRole.Admin), (SeedIds.Users.Priya, ProjectRole.Member));
    }

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?> { ["SHIPLEAN_GITHUB_TOKEN"] = "ghp_settingsfixture" };

    [Test]
    public async Task Get_WhenMember_ShouldReturnContractShape()
    {
        var project = await ArrangeAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await ArrangeIntakeAsync(database, project, "https://github.com/ShipLean/ShipLean", withConnection: true);
            await database.ExecuteAsync(
                "INSERT INTO project_issue_intake (project_id, enabled, repository, run_as_user_id) VALUES (@id, true, 'ShipLean/ShipLean', @priya)",
                new { id = project.Id, priya = SeedIds.Users.Priya });
        }

        var pinned = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "issueIntake", "issue-intake-settings.json")));

        using var response = await Api.ActingAs(TestUser.Priya).GetAsync(PathOf(project));

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        JsonNode.DeepEquals(JsonNode.Parse(text), pinned).ShouldBeTrue(text);
    }

    [Test]
    public async Task Get_WhenMember_ShouldReportReadiness()
    {
        var bare = await ArrangeAsync();
        var ready = await ArrangeAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await ArrangeIntakeAsync(database, ready, "git@github.com:Acme/Widgets.git", withConnection: true);
            await ArrangeIntakeAsync(database, bare, "https://gitlab.com/acme/widgets", withConnection: false);
        }

        var notReady = await ReadAsync(bare);
        var isReady = await ReadAsync(ready);

        notReady.Repository.ShouldBeNull();
        notReady.Readiness.ShouldBe(new IssueIntakeReadiness(HasGitHubRepository: false, HasAgentConnection: false, HasGitHubToken: true, HasWebhookSecret: false, HasRunAs: false));
        isReady.Repository.ShouldBe("Acme/Widgets");
        isReady.Readiness.ShouldBe(new IssueIntakeReadiness(HasGitHubRepository: true, HasAgentConnection: true, HasGitHubToken: true, HasWebhookSecret: false, HasRunAs: false));
    }

    [Test]
    public async Task Put_WhenEnabled_ShouldAddTriageStatesOnce()
    {
        var project = await ArrangeAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await ArrangeIntakeAsync(database, project, "https://github.com/Acme/Widgets", withConnection: false);
        }

        using var first = await Api.PutJsonAsync(PathOf(project), Save(enabled: true, SeedIds.Users.Priya));
        using var second = await Api.PutJsonAsync(PathOf(project), Save(enabled: true, SeedIds.Users.Priya));

        first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        second.StatusCode.ShouldBe(HttpStatusCode.OK, await second.Content.ReadAsStringAsync());
        await using var check = await OpenDatabaseAsync();
        var triage = (await check.QueryAsync<(string Type, string Category, int Order, int Lowest)>(
            """
            SELECT s.type, s.category, s.sort_order AS "Order", (SELECT min(o.sort_order) FROM state o WHERE o.project_id = s.project_id AND o.type = s.type AND o.archived_at IS NULL) AS lowest
            FROM state s WHERE s.project_id = @id AND s.is_intake ORDER BY s.type
            """, new { id = project.Id })).ToList();
        triage.Select(t => t.Type).ShouldBe(["bug", "feature", "story"]);
        triage.ShouldAllBe(t => t.Category == "proposed" && t.Order == t.Lowest);
    }

    [Test]
    public async Task Put_WhenConcurrentEnable_ShouldAllowOne()
    {
        var first = await ArrangeAsync();
        var second = await ArrangeAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await ArrangeIntakeAsync(database, first, "https://github.com/Acme/Race", withConnection: false);
            await ArrangeIntakeAsync(database, second, "git@github.com:acme/race.git", withConnection: false);
        }

        var responses = await Task.WhenAll(
            Api.PutJsonAsync(PathOf(first), Save(enabled: true, SeedIds.Users.Priya)),
            Api.PutJsonAsync(PathOf(second), Save(enabled: true, SeedIds.Users.Priya)));

        responses.Select(r => r.StatusCode).Order().ShouldBe([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        var refused = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        var error = await ApiClient.ReadApiErrorAsync(refused, HttpStatusCode.Conflict);
        error.Code.ShouldBe(ApiErrorCodes.RepositoryTaken);
        error.Field.ShouldBe("enabled");
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Test]
    public async Task Put_WhenRepositoryTaken_ShouldReturnError()
    {
        var holder = await ArrangeAsync();
        var other = await ArrangeAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await ArrangeIntakeAsync(database, holder, "https://github.com/Acme/Taken", withConnection: false);
            await ArrangeIntakeAsync(database, other, "https://github.com/acme/TAKEN.git", withConnection: false);
        }

        using var held = await Api.PutJsonAsync(PathOf(holder), Save(enabled: true, SeedIds.Users.Priya));
        using var refused = await Api.PutJsonAsync(PathOf(other), Save(enabled: true, SeedIds.Users.Priya));

        held.StatusCode.ShouldBe(HttpStatusCode.OK, await held.Content.ReadAsStringAsync());
        (await ApiClient.ReadApiErrorAsync(refused, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.RepositoryTaken);
        await using var check = await OpenDatabaseAsync();
        (await check.ExecuteScalarAsync<int>("SELECT count(*)::int FROM state WHERE project_id = @id AND is_intake", new { id = other.Id })).ShouldBe(0);
    }

    [Test]
    public async Task Put_WhenRunAsNotMember_ShouldReturnError()
    {
        var project = await ArrangeAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await ArrangeIntakeAsync(database, project, "https://github.com/Acme/NoRunAs", withConnection: false);
        }

        using var stranger = await Api.PutJsonAsync(PathOf(project), Save(enabled: true, SeedIds.Users.Kai));
        using var none = await Api.PutJsonAsync(PathOf(project), Save(enabled: true, null));

        var notMember = await ApiClient.ReadApiErrorAsync(stranger, HttpStatusCode.BadRequest);
        notMember.Code.ShouldBe(ApiErrorCodes.NotMember);
        notMember.Field.ShouldBe("runAsUserId");
        (await ApiClient.ReadApiErrorAsync(none, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.NotMember);
        await using var check = await OpenDatabaseAsync();
        (await check.ExecuteScalarAsync<int>("SELECT count(*)::int FROM project_issue_intake WHERE project_id = @id", new { id = project.Id })).ShouldBe(0);
    }

    [Test]
    public async Task Put_WhenNoGitHubRepoUrl_ShouldReturnError()
    {
        var project = await ArrangeAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await ArrangeIntakeAsync(database, project, "https://gitlab.com/acme/widgets", withConnection: false);
        }

        using var refused = await Api.PutJsonAsync(PathOf(project), Save(enabled: true, SeedIds.Users.Priya));

        (await ApiClient.ReadApiErrorAsync(refused, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.NoRepoUrl);
    }

    [Test]
    public async Task SaveGeneral_WhenRepoChangesWhileOn_ShouldRefuse()
    {
        var project = await ArrangeAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await ArrangeIntakeAsync(database, project, "https://github.com/Acme/Locked", withConnection: false);
        }

        using var on = await Api.PutJsonAsync(PathOf(project), Save(enabled: true, SeedIds.Users.Priya));
        using var moved = await Api.PutJsonAsync($"api/projects/{project.Id}/settings", await GeneralBodyAsync(project, "https://github.com/Acme/Elsewhere"));
        using var respelled = await Api.PutJsonAsync($"api/projects/{project.Id}/settings", await GeneralBodyAsync(project, "git@github.com:acme/locked.git"));

        on.StatusCode.ShouldBe(HttpStatusCode.OK, await on.Content.ReadAsStringAsync());
        var error = await ApiClient.ReadApiErrorAsync(moved, HttpStatusCode.Conflict);
        error.Code.ShouldBe(ApiErrorCodes.IntakeOn);
        error.Field.ShouldBe("repoUrl");
        respelled.StatusCode.ShouldBe(HttpStatusCode.OK, await respelled.Content.ReadAsStringAsync());
    }

    private async Task<JsonElement> GeneralBodyAsync(ArrangedProject project, string repoUrl)
    {
        using var response = await Api.GetAsync($"api/projects/{project.Id}/settings");
        var loaded = JsonSerializer.Deserialize<ProjectSettingsDto>(await response.Content.ReadAsStringAsync(), WireOptions)!;
        var request = new ProjectSettingsRequest(
            new ProjectGeneralRequest(loaded.General.Key, loaded.General.Name, loaded.General.Description, loaded.General.WorkingDays, loaded.General.AgentConnectionId, repoUrl, loaded.General.RepoBaseBranch),
            [.. loaded.Members.Select(m => new MemberPut(m.Email, m.Role))],
            loaded.Sprints,
            new StateChanges([], [], [], []));
        return JsonSerializer.SerializeToElement(request, WireOptions);
    }

    private async Task<IssueIntakeSettingsDto> ReadAsync(ArrangedProject project)
    {
        using var response = await Api.GetAsync(PathOf(project));
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<IssueIntakeSettingsDto>(text, JsonSerializerOptions.Web)!;
    }

    private static object Save(bool enabled, Guid? runAs) => new { enabled, runAsUserId = runAs, proposalInstructions = "", maxRounds = 5 };

    private static async Task ArrangeIntakeAsync(NpgsqlConnection database, ArrangedProject project, string repoUrl, bool withConnection)
    {
        Guid? connection = null;
        if (withConnection)
        {
            connection = Guid.NewGuid();
            await database.ExecuteAsync(
                """
                INSERT INTO agent_connection (id, type, name, receiver_url, signing_secret_protected, signing_secret_last4, callback_token_hash, callback_token_last4, created_at, updated_at)
                VALUES (@id, 'linear-compatible', 'Settings relay', 'https://relay.example.com/hook', 'x', 'xxxx', @hash, 'xxxx', now(), now())
                """,
                new { id = connection, hash = connection.Value.ToByteArray() });
        }

        await database.ExecuteAsync("UPDATE project SET repo_url = @repoUrl, agent_connection_id = @connection WHERE id = @id", new { id = project.Id, repoUrl, connection });
    }

    [Test]
    public async Task Get_WhenNotMember_ShouldBeNotFound()
    {
        var project = await ArrangeAsync();

        using var response = await Api.ActingAs(TestUser.Kai).GetAsync(PathOf(project));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
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
