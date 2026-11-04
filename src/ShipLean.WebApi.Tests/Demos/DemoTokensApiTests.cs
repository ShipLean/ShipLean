using System.Net;
using System.Text.Json;
using Dapper;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Demos;

// K18's tokens over the real route and store. Every test arranges a project of its own, Joel its admin and Priya a member. What is stored is the
// normalised lines, a refused text changes nothing, and only an admin may write.
[TestFixture]
public class DemoTokensApiTests : ApiFixture
{
    private async Task<ArrangedProject> ArrangeAsync()
    {
        await using var database = await OpenDatabaseAsync();
        return await ArrangedProject.CreateAsync(database, (SeedIds.Users.Joel, ProjectRole.Admin), (SeedIds.Users.Priya, ProjectRole.Member));
    }

    private static string Route(ArrangedProject project) => $"api/projects/{project.Id}/demo-brand/tokens";

    private Task<HttpResponseMessage> PutAsync(ArrangedProject project, string? tokens, ApiClient? api = null)
        => (api ?? Api).PutJsonAsync(Route(project), new { tokens });

    private async Task<string?> StoredAsync(ArrangedProject project)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleOrDefaultAsync<string?>("SELECT tokens FROM project_demo_settings WHERE project_id = @id", new { id = project.Id });
    }

    private static async Task<string> TokensOfAsync(HttpResponseMessage response)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
            return JsonDocument.Parse(text).RootElement.GetProperty("tokens").GetString()!;
        }
    }

    [Test]
    public async Task PutTokens_WhenValid_ShouldStoreTheNormalisedLines()
    {
        var project = await ArrangeAsync();

        var returned = await TokensOfAsync(await PutAsync(project, "  --color-bg:#ABCDEF ;\r\n\r\n--font-sans: inter, 'SEGOE   ui', SERIF;\n--radius: 4PX;"));

        const string normalised = "--color-bg: #abcdef;\n--font-sans: \"Inter\", \"Segoe UI\", serif;\n--radius: 4px;";
        returned.ShouldBe(normalised);
        (await StoredAsync(project)).ShouldBe(normalised);
        using var settings = await Api.GetAsync($"api/projects/{project.Id}/demo-settings");
        JsonDocument.Parse(await settings.Content.ReadAsStringAsync()).RootElement.GetProperty("brand").GetProperty("tokens").GetString().ShouldBe(normalised);
    }

    [Test]
    public async Task PutTokens_WhenRefused_ShouldNameTheLineAndKeepTheOldOnes()
    {
        var project = await ArrangeAsync();
        await TokensOfAsync(await PutAsync(project, "--color-accent: #123456;"));

        using var refused = await PutAsync(project, "--color-bg: #ffffff;\n--color-text: url(https://evil.example/x.png);");

        var error = await ApiClient.ReadApiErrorAsync(refused, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("tokens");
        error.Error.ShouldStartWith("Line 2:");
        (await StoredAsync(project)).ShouldBe("--color-accent: #123456;");
    }

    [Test]
    public async Task PutTokens_WhenMember_ShouldBeForbidden()
    {
        var project = await ArrangeAsync();
        await TokensOfAsync(await PutAsync(project, "--color-accent: #123456;"));

        using var refused = await PutAsync(project, "--color-accent: #654321;", Api.ActingAs(TestUser.Priya));

        (await ApiClient.ReadApiErrorAsync(refused, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
        (await StoredAsync(project)).ShouldBe("--color-accent: #123456;");
    }

    [Test]
    public async Task PutTokens_WhenBlank_ShouldClearThem()
    {
        var project = await ArrangeAsync();
        await TokensOfAsync(await PutAsync(project, "--color-accent: #123456;"));

        (await TokensOfAsync(await PutAsync(project, " \n "))).ShouldBe(string.Empty);

        (await StoredAsync(project)).ShouldBe(string.Empty);
    }

    [Test]
    public async Task PutTokens_WhenTheProjectHasALogo_ShouldLeaveTheLogoAlone()
    {
        var project = await ArrangeAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("INSERT INTO project_demo_settings (project_id, logo_blob, logo_version) VALUES (@id, 'demo-logos/kept', 4)", new { id = project.Id });
        }

        await TokensOfAsync(await PutAsync(project, "--color-accent: #123456;"));

        await using var check = await OpenDatabaseAsync();
        (await check.QuerySingleAsync<(string? Blob, int Version)>("SELECT logo_blob, logo_version FROM project_demo_settings WHERE project_id = @id", new { id = project.Id }))
            .ShouldBe(("demo-logos/kept", 4));
    }
}
