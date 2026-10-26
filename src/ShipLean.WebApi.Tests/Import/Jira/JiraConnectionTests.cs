using System.Net;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Import.Jira;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Database;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Jira;
using Shouldly;

namespace ShipLean.WebApi.Tests.Import.Jira;

// One seeded scenario for the Jira connection endpoints on SHIP, whose admin is Joel. The Jira is the in-process stub; the tests run in order because each saves a connection over the last.
[TestFixture]
public sealed class JiraConnectionTests : ApiFixture
{
    private const string SecretToken = "tok-SECRET-9f8e7d6c-WXYZ";

    private static readonly string Connection = $"api/projects/{SeedIds.Projects.Ship}/jira/connection";

    private readonly JiraStubHandler _stub = new();
    private readonly LogCapture _logs = new();

    private TextWriter? _console;

    // The host's console sink writes to Console.Out; swapping it before the host starts keeps every log line for the token check.
    protected override Task<ClonedDatabase> CloneDatabaseAsync()
    {
        _console = Console.Out;
        Console.SetOut(_logs.Writer);
        return base.CloneDatabaseAsync();
    }

    [OneTimeTearDown]
    public void RestoreConsole()
    {
        if (_console is not null)
        {
            Console.SetOut(_console);
        }
    }

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        JiraStubHost.Pin(services, _stub);
    };

    private static object Cloud(string token = JiraStubHandler.CloudToken, string? baseUrl = null)
        => new { baseUrl = baseUrl ?? JiraStubHandler.CloudUrl, kind = "cloud", email = JiraStubHandler.CloudEmail, token };

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<T>(text, ReadOptions)!;
    }

    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Test, Order(1)]
    public async Task Jira_WhenNoConnectionSaved_ShouldReadNullAndTestShouldSayWhy()
    {
        using var read = await Api.GetAsync(Connection);
        using var test = await Api.PostJsonAsync(Connection + "/test");
        using var projects = await Api.GetAsync($"api/projects/{SeedIds.Projects.Ship}/jira/projects");

        read.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var result = await ReadAsync<JiraTestResultDto>(test);
        result.Ok.ShouldBeFalse();
        result.Message.ShouldNotBeNullOrWhiteSpace();
        (await ApiClient.ReadApiErrorAsync(projects, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test, Order(2)]
    public async Task Jira_WhenTestConnection_ShouldReturnWho()
    {
        using var saved = await Api.PutJsonAsync(Connection, Cloud());
        var connection = await ReadAsync<JiraConnectionDto>(saved);

        using var test = await Api.PostJsonAsync(Connection + "/test");

        connection.Kind.ShouldBe(JiraKind.Cloud);
        connection.BaseUrl.ShouldBe(JiraStubHandler.CloudUrl);
        connection.CreatedBy.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Joel);
        var result = await ReadAsync<JiraTestResultDto>(test);
        result.Ok.ShouldBeTrue();
        result.Who.ShouldNotBeNull().DisplayName.ShouldBe("Priya Shah");
        result.Who.Email.ShouldBe("priya.shah@acme.io");
        result.Message.ShouldBeNull();
    }

    [Test, Order(3)]
    public async Task Jira_WhenServerConnectionSaved_ShouldTestWithBearer()
    {
        using var saved = await Api.PutJsonAsync(Connection, new { baseUrl = JiraStubHandler.ServerUrl, kind = "server", email = string.Empty, token = JiraStubHandler.ServerToken });
        var connection = await ReadAsync<JiraConnectionDto>(saved);

        using var test = await Api.PostJsonAsync(Connection + "/test");

        connection.Kind.ShouldBe(JiraKind.Server);
        (await ReadAsync<JiraTestResultDto>(test)).Ok.ShouldBeTrue();
    }

    [Test, Order(4)]
    public async Task Jira_WhenTokenIsWrong_ShouldTestToAMessageNotAnError()
    {
        using var saved = await Api.PutJsonAsync(Connection, Cloud(token: "expired-token-1234"));
        saved.EnsureSuccessStatusCode();

        using var test = await Api.PostJsonAsync(Connection + "/test");

        var result = await ReadAsync<JiraTestResultDto>(test);
        result.Ok.ShouldBeFalse();
        result.Who.ShouldBeNull();
        result.Message.ShouldStartWith("Jira refused the email and token");
    }

    [Test, Order(5)]
    public async Task Jira_WhenJiraIsDown_ShouldTestToAMessageNotAnError()
    {
        using var saved = await Api.PutJsonAsync(Connection, Cloud());
        saved.EnsureSuccessStatusCode();
        _stub.IsDown = true;

        using var test = await Api.PostJsonAsync(Connection + "/test");
        _stub.IsDown = false;

        var result = await ReadAsync<JiraTestResultDto>(test);
        result.Ok.ShouldBeFalse();
        result.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Test, Order(6)]
    public async Task Jira_WhenTokenSaved_ShouldNeverBeReadBack()
    {
        using var saved = await Api.PutJsonAsync(Connection, Cloud(token: SecretToken));
        var savedBody = await saved.Content.ReadAsStringAsync();
        using var read = await Api.GetAsync(Connection);
        var readBody = await read.Content.ReadAsStringAsync();
        using var test = await Api.PostJsonAsync(Connection + "/test");
        var testBody = await test.Content.ReadAsStringAsync();
        using var projects = await Api.GetAsync($"api/projects/{SeedIds.Projects.Ship}/jira/projects");
        var projectsBody = await projects.Content.ReadAsStringAsync();

        foreach (var body in new[] { savedBody, readBody, testBody, projectsBody })
        {
            body.ShouldNotContain(SecretToken);
        }
        JsonSerializer.Deserialize<JiraConnectionDto>(readBody, ReadOptions)!.TokenLastFour.ShouldBe("WXYZ");
        await using var database = await OpenDatabaseAsync();
        var stored = await database.QuerySingleAsync<string>("SELECT api_token_encrypted FROM jira_connection WHERE project_id = @id AND archived_at IS NULL", new { id = SeedIds.Projects.Ship });
        stored.ShouldNotBeNullOrWhiteSpace();
        stored.ShouldNotContain(SecretToken);
        stored.ShouldNotContain("WXYZ");
        _logs.Text.ShouldNotBeNullOrWhiteSpace();
        _logs.Text.ShouldNotContain(SecretToken);
    }

    [Test, Order(7)]
    public async Task Jira_WhenReplaced_ShouldArchiveTheOldRowAndServeTheNewCredentials()
    {
        await using var database = await OpenDatabaseAsync();
        var before = await database.QuerySingleAsync<Guid>("SELECT id FROM jira_connection WHERE project_id = @id AND archived_at IS NULL", new { id = SeedIds.Projects.Ship });

        using var saved = await Api.PutJsonAsync(Connection, Cloud(token: "replacement-token-5678"));
        var replacement = await ReadAsync<JiraConnectionDto>(saved);

        replacement.Id.ShouldNotBe(before);
        replacement.TokenLastFour.ShouldBe("5678");
        var rows = (await database.QueryAsync<(Guid Id, DateTime? ArchivedAt)>("SELECT id, archived_at FROM jira_connection WHERE project_id = @id", new { id = SeedIds.Projects.Ship })).ToList();
        rows.Where(r => r.ArchivedAt is null).Select(r => r.Id).ShouldBe([replacement.Id]);
        rows.Single(r => r.Id == before).ArchivedAt.ShouldNotBeNull();
        (await database.QuerySingleAsync<string?>("SELECT api_token_encrypted FROM jira_connection WHERE id = @before", new { before })).ShouldBeNull();
        using var scope = Host.Services.CreateScope();
        var credentials = scope.ServiceProvider.GetRequiredService<IJiraConnections>();
        (await credentials.GetCredentialsAsync(replacement.Id)).Token.ShouldBe("replacement-token-5678");
        (await Should.ThrowAsync<ApiException>(() => credentials.GetCredentialsAsync(before))).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test, Order(8)]
    public async Task Jira_WhenProjectsListed_ShouldCarryImportedAsFromTheLatestFinishedJob()
    {
        using var saved = await Api.PutJsonAsync(Connection, Cloud());
        var connection = await ReadAsync<JiraConnectionDto>(saved);
        await using var database = await OpenDatabaseAsync();
        var jobId = Guid.NewGuid();
        await database.ExecuteAsync(
            """
            INSERT INTO import_job (id, connection_id, jira_project_key, project_key, project_id, status, progress, finished_at)
            VALUES (@jobId, @connectionId, 'PLATFORM', 'PLAT', NULL, 'done', '{}'::jsonb, now())
            """,
            new { jobId, connectionId = connection.Id });

        using var response = await Api.GetAsync($"api/projects/{SeedIds.Projects.Ship}/jira/projects");

        var projects = await ReadAsync<JiraProjectDto[]>(response);
        projects.Select(p => p.Key).ShouldBe(["PLATFORM", "INFRA"]);
        var platform = projects.Single(p => p.Key == "PLATFORM");
        platform.IssueCount.ShouldBe(JiraStubFiles.IssueKeys.Count);
        platform.ImportedAs.ShouldNotBeNull().JobId.ShouldBe(jobId);
        platform.ImportedAs.ProjectKey.ShouldBe("PLAT");
        platform.ImportedAs.FinishedLabel.ShouldNotBeNullOrWhiteSpace();
        projects.Single(p => p.Key == "INFRA").ImportedAs.ShouldBeNull();
    }

    [Test, Order(8)]
    public async Task Jira_WhenAJobFailedOrIsRunning_ShouldNotCountAsImported()
    {
        await using var database = await OpenDatabaseAsync();
        var connectionId = await database.QuerySingleAsync<Guid>("SELECT id FROM jira_connection WHERE project_id = @id AND archived_at IS NULL", new { id = SeedIds.Projects.Ship });
        await database.ExecuteAsync(
            """
            INSERT INTO import_job (id, connection_id, jira_project_key, project_key, project_id, status, progress, started_at, finished_at)
            VALUES (@failed, @connectionId, 'INFRA', 'INF', NULL, 'failed', '{}'::jsonb, now(), now()),
                   (@running, @connectionId, 'INFRA', 'INF', NULL, 'running', '{}'::jsonb, now(), NULL),
                   (@errors, @connectionId, 'PLATFORM', 'PLAT', NULL, 'doneWithErrors', '{}'::jsonb, now(), now() + interval '1 minute')
            """,
            new { failed = Guid.NewGuid(), running = Guid.NewGuid(), errors = Guid.NewGuid(), connectionId });

        using var response = await Api.GetAsync($"api/projects/{SeedIds.Projects.Ship}/jira/projects");

        var projects = await ReadAsync<JiraProjectDto[]>(response);
        projects.Single(p => p.Key == "INFRA").ImportedAs.ShouldBeNull();
        projects.Single(p => p.Key == "PLATFORM").ImportedAs.ShouldNotBeNull();
    }

    [Test, Order(9)]
    public async Task Jira_WhenCallerIsNotAnAdmin_ShouldBeForbiddenOrNotFound()
    {
        using var asMember = await Api.ActingAs(TestUser.Mei).PutJsonAsync(Connection, Cloud());
        using var asOutsider = await Api.ActingAs(TestUser.Kai).GetAsync(Connection);

        (await ApiClient.ReadApiErrorAsync(asMember, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
        (await ApiClient.ReadApiErrorAsync(asOutsider, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [TestCase("https://10.0.0.5")]
    [TestCase("https://localhost")]
    [TestCase("https://169.254.169.254")]
    [TestCase("http://jira.example.com")]
    public async Task Jira_WhenBaseUrlIsPrivate_ShouldBeValidation(string baseUrl)
    {
        using var response = await Api.PutJsonAsync(Connection, Cloud(baseUrl: baseUrl));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [TestCase("""{"baseUrl":"https://x.atlassian.net","kind":"cloud","email":"no-at-sign","token":"t"}""")]
    [TestCase("""{"baseUrl":"https://x.atlassian.net","kind":"server","email":"","token":"  "}""")]
    [TestCase("""{"baseUrl":"","kind":"server","email":"","token":"t"}""")]
    public async Task Jira_WhenSaveIsIncomplete_ShouldBeValidation(string json)
    {
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await Api.Http.SendAsync(new HttpRequestMessage(HttpMethod.Put, Connection) { Content = content, Headers = { { ClientHeader.Name, ApiClient.ClientId } } });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }
}
