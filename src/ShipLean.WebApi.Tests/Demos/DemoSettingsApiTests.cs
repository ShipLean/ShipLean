using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Instance.AgentConnections;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Demos;

// K16 over the real route, store and agent check. Every test arranges a project of its own, Joel its admin and Priya a member. Brand and YouTube
// are replaced by fakes with distinctive values, so a part that was dropped or defaulted shows.
[TestFixture]
public class DemoSettingsApiTests : ApiFixture
{
    private const string NoConnection = "This project has no agent connection. Choose one in the project's General settings.";
    private const string NoRepository = "This project has no repository. Add its URL in the project's General settings.";
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    private readonly DemoEndpointFakes.DemoBrandFake _brand = new();
    private readonly FakeYouTubeConnections _youTube = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        _brand.Read.Returns(new ProjectDemoBrandDto("/api/projects/x/demo-brand/logo?v=7", "image/svg+xml", "--color-accent: #123456;", [new DemoDefaultTokenDto("--color-bg", "#abcdef")]));
        services.RemoveAll<IDemoBrand>();
        services.AddSingleton<IDemoBrand>(_brand);
        services.RemoveAll<IYouTubeConnections>();
        services.AddSingleton<IYouTubeConnections>(_youTube);
    };

    private async Task<ArrangedProject> ArrangeAsync(bool connection = true, bool repository = true)
    {
        await using var database = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(database, (SeedIds.Users.Joel, ProjectRole.Admin), (SeedIds.Users.Priya, ProjectRole.Member));
        Guid? connectionId = connection ? (await AgentConnectionsWire.CreateAsync(Api, $"Relay {Guid.NewGuid():N}")).Connection.Id : null;
        await database.ExecuteAsync(
            "UPDATE project SET agent_connection_id = @connectionId, repo_url = @repo WHERE id = @id",
            new { id = project.Id, connectionId, repo = repository ? "https://github.com/ShipLean/ShipLean.git" : null });
        return project;
    }

    private static string Route(ArrangedProject project) => $"api/projects/{project.Id}/demo-settings";

    private async Task<ProjectDemoSettingsDto> GetAsync(ArrangedProject project, ApiClient? api = null)
    {
        using var response = await (api ?? Api).GetAsync(Route(project));
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<ProjectDemoSettingsDto>(text, WireOptions)!;
    }

    private async Task<HttpResponseMessage> PutAsync(ArrangedProject project, bool automatic, string? environment, ApiClient? api = null)
        => await (api ?? Api).PutJsonAsync(Route(project), new { automatic, environment });

    private static async Task<ProjectDemoSettingsDto> ReadOkAsync(HttpResponseMessage response)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
            return JsonSerializer.Deserialize<ProjectDemoSettingsDto>(text, WireOptions)!;
        }
    }

    private async Task<(bool Automatic, string Environment)?> StoredAsync(ArrangedProject project)
    {
        await using var database = await OpenDatabaseAsync();
        var row = await database.QuerySingleOrDefaultAsync<(bool Automatic, string Environment)?>(
            "SELECT automatic, environment FROM project_demo_settings WHERE project_id = @id", new { id = project.Id });
        return row;
    }

    [Test]
    public async Task Get_WhenNoRow_ShouldBeOffAndBlank()
    {
        var project = await ArrangeAsync();

        var settings = await GetAsync(project);

        settings.Automatic.ShouldBeFalse();
        settings.Environment.ShouldBe(string.Empty);
        settings.AgentCheck.ShouldBe(new DemoAgentCheckDto(true, null, "Ready to make demos."));
    }

    [Test]
    public async Task Put_WhenAdmin_ShouldSave()
    {
        var project = await ArrangeAsync();

        var saved = await ReadOkAsync(await PutAsync(project, true, "Run it with make dev-seeded."));

        saved.Automatic.ShouldBeTrue();
        saved.Environment.ShouldBe("Run it with make dev-seeded.");
        (await StoredAsync(project)).ShouldBe((true, "Run it with make dev-seeded."));
        (await GetAsync(project)).Environment.ShouldBe("Run it with make dev-seeded.");
    }

    [Test]
    public async Task Put_WhenMember_ShouldBeForbiddenByTheRole()
    {
        var project = await ArrangeAsync();
        var priya = Api.ActingAs(TestUser.Priya);

        using var refused = await PutAsync(project, true, "Member text", priya);
        var saved = await ReadOkAsync(await PutAsync(project, true, "Admin text"));

        (await ApiClient.ReadApiErrorAsync(refused, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
        saved.Environment.ShouldBe("Admin text");
        (await GetAsync(project, priya)).Environment.ShouldBe("Admin text");
    }

    [Test]
    public async Task Put_WhenAutomaticAndNoConnection_ShouldBeValidationOnAutomatic()
    {
        var project = await ArrangeAsync(connection: false);

        using var refused = await PutAsync(project, true, "Refused text");
        var error = await ApiClient.ReadApiErrorAsync(refused, HttpStatusCode.BadRequest);
        var control = await ReadOkAsync(await PutAsync(project, false, "Kept text"));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("automatic");
        error.Error.ShouldBe(NoConnection);
        control.Environment.ShouldBe("Kept text");
        control.AgentCheck.ShouldBe(new DemoAgentCheckDto(false, AgentStartBlock.NoConnection, NoConnection));
        (await StoredAsync(project)).ShouldBe((false, "Kept text"));
    }

    [Test]
    public async Task Put_WhenAutomaticAndNoRepository_ShouldBeValidationOnAutomatic()
    {
        var project = await ArrangeAsync(repository: false);

        using var refused = await PutAsync(project, true, "Refused text");
        var error = await ApiClient.ReadApiErrorAsync(refused, HttpStatusCode.BadRequest);
        var control = await ReadOkAsync(await PutAsync(project, false, "Kept text"));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("automatic");
        error.Error.ShouldBe(NoRepository);
        control.AgentCheck.ShouldBe(new DemoAgentCheckDto(false, AgentStartBlock.NoRepository, NoRepository));
        (await StoredAsync(project)).ShouldBe((false, "Kept text"));
    }

    [Test]
    public async Task Put_WhenAgentReady_ShouldTurnOn()
    {
        var project = await ArrangeAsync();

        var saved = await ReadOkAsync(await PutAsync(project, true, null));

        saved.Automatic.ShouldBeTrue();
        saved.Environment.ShouldBe(string.Empty);
        saved.AgentCheck.Ready.ShouldBeTrue();
        (await StoredAsync(project)).ShouldBe((true, string.Empty));
    }

    [Test]
    public async Task Put_WhenEnvironmentTooLong_ShouldBeValidation()
    {
        var project = await ArrangeAsync();

        using var refused = await PutAsync(project, false, new string('e', 4001));
        var error = await ApiClient.ReadApiErrorAsync(refused, HttpStatusCode.BadRequest);
        var control = await ReadOkAsync(await PutAsync(project, false, new string('e', 4000)));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("environment");
        control.Environment.Length.ShouldBe(4000);
    }

    [Test]
    public async Task Get_ShouldCarryTheBrandAndYouTubeParts()
    {
        var project = await ArrangeAsync();
        _youTube.Check = new YouTubeProjectDto(YouTubeConnectionStatus.Connected, "Distinct Channel", YouTubeVisibility.Unlisted, "Checked at distinct time", null);

        var settings = await GetAsync(project);

        settings.Brand.LogoUrl.ShouldBe("/api/projects/x/demo-brand/logo?v=7");
        settings.Brand.Tokens.ShouldBe("--color-accent: #123456;");
        settings.Brand.DefaultTokens.ShouldBe([new DemoDefaultTokenDto("--color-bg", "#abcdef")]);
        settings.YouTube.ChannelTitle.ShouldBe("Distinct Channel");
        settings.YouTube.CheckedLabel.ShouldBe("Checked at distinct time");
        _youTube.Reset();
    }

    [Test]
    public async Task Put_ShouldLeaveTheLogoAndTokensAlone()
    {
        var project = await ArrangeAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync(
                "INSERT INTO project_demo_settings (project_id, logo_blob, logo_version, tokens) VALUES (@id, 'logo-key-1', 3, '--color-accent: #010203;')", new { id = project.Id });
        }

        await ReadOkAsync(await PutAsync(project, true, "New environment"));

        await using var check = await OpenDatabaseAsync();
        var row = await check.QuerySingleAsync<(string? Blob, int Version, string Tokens, bool Automatic)>(
            "SELECT logo_blob, logo_version, tokens, automatic FROM project_demo_settings WHERE project_id = @id", new { id = project.Id });
        row.ShouldBe(("logo-key-1", 3, "--color-accent: #010203;", true));
    }

    [Test]
    public async Task Get_WhenConnectionLaterArchived_ShouldKeepAutomaticAndReportTheCheck()
    {
        var project = await ArrangeAsync();
        await ReadOkAsync(await PutAsync(project, true, "Env"));
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync(
                "UPDATE agent_connection SET archived_at = now() WHERE id = (SELECT agent_connection_id FROM project WHERE id = @id)", new { id = project.Id });
        }

        var settings = await GetAsync(project);

        settings.Automatic.ShouldBeTrue();
        settings.AgentCheck.ShouldBe(new DemoAgentCheckDto(false, AgentStartBlock.NoConnection, NoConnection));
    }
}
