using System.Net;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Instance.Integrations;

// One seeded scenario, no environment settings: saving the integrations, in order.
[TestFixture]
public class IntegrationsSaveTests : ApiFixture
{
    private const string GitHubSecret = "ghp_topsecret1234";
    private const string GitLabSecret = "glpat-topsecret5678";

    [Test]
    [Order(1)]
    public async Task Integrations_WhenSaved_ShouldNeverReturnToken()
    {
        using var saved = await Api.PutJsonAsync("api/instance/integrations", new
        {
            gitHubToken = GitHubSecret,
            gitLabUrl = "https://gitlab.example.com/",
            gitLabToken = GitLabSecret,
            previewAllowHosts = new[] { " Example.COM ", "", "docs.example.org", "example.com" },
        });

        var (dto, text) = await IntegrationsWire.ReadAsync(saved);
        var (read, readText) = await IntegrationsWire.ReadAsync(await Api.GetAsync("api/instance/integrations"));
        read.PreviewAllowHosts.ShouldBe(dto.PreviewAllowHosts);
        read.GitLabTokenLastFour.ShouldBe(dto.GitLabTokenLastFour);
        dto.GitHubTokenLastFour.ShouldBe("1234");
        dto.GitHubTokenFromEnv.ShouldBeFalse();
        dto.GitLabUrl.ShouldBe("https://gitlab.example.com");
        dto.GitLabTokenLastFour.ShouldBe("5678");
        dto.PreviewAllowHosts.ShouldBe(["example.com", "docs.example.org"]);
        foreach (var body in new[] { text, readText })
        {
            body.ShouldNotContain("topsecret");
            body.ShouldNotContain("ghp_");
            body.ShouldNotContain("glpat");
        }
        await using var connection = await OpenDatabaseAsync();
        var stored = await Dapper.SqlMapper.QuerySingleAsync<string>(connection, "SELECT github_token FROM instance_settings");
        stored.ShouldNotContain("topsecret");
        var resolved = await IntegrationsWire.ResolveAsync(Host);
        resolved.GitHubToken.ShouldBe(GitHubSecret);
        resolved.GitLabToken.ShouldBe(GitLabSecret);
        resolved.GitLabUrl.ShouldBe("https://gitlab.example.com");
        resolved.PreviewAllowHosts.ShouldBe(["example.com", "docs.example.org"]);
    }

    [Test]
    [Order(2)]
    public async Task Integrations_WhenGitLabUrlSentUnchanged_ShouldKeepTheStoredToken()
    {
        using var saved = await Api.PutJsonAsync("api/instance/integrations", new { gitLabUrl = "https://gitlab.example.com" });

        (await IntegrationsWire.ReadAsync(saved)).Dto.GitLabTokenLastFour.ShouldBe("5678");
        (await IntegrationsWire.ResolveAsync(Host)).GitLabToken.ShouldBe(GitLabSecret);
    }

    [Test]
    [Order(3)]
    public async Task Integrations_WhenGitLabUrlChanged_ShouldClearTheStoredToken()
    {
        using var saved = await Api.PutJsonAsync("api/instance/integrations", new { gitLabUrl = "https://gitlab.other.example.com" });

        var (dto, text) = await IntegrationsWire.ReadAsync(saved);
        dto.GitLabUrl.ShouldBe("https://gitlab.other.example.com");
        dto.GitLabTokenLastFour.ShouldBeNull();
        text.ShouldNotContain("5678");
        var resolved = await IntegrationsWire.ResolveAsync(Host);
        resolved.GitLabUrl.ShouldBe("https://gitlab.other.example.com");
        resolved.GitLabToken.ShouldBeNull();
        resolved.GitHubToken.ShouldBe(GitHubSecret);
    }

    [Test]
    [Order(4)]
    public async Task Integrations_WhenCleared_ShouldRemoveTheSavedValues()
    {
        using var saved = await Api.PutJsonAsync("api/instance/integrations", new { clearGitHubToken = true, gitLabUrl = "", previewAllowHosts = Array.Empty<string>() });

        var dto = (await IntegrationsWire.ReadAsync(saved)).Dto;
        dto.GitHubTokenLastFour.ShouldBeNull();
        dto.GitLabUrl.ShouldBeNull();
        dto.PreviewAllowHosts.ShouldBeEmpty();
        (await IntegrationsWire.ResolveAsync(Host)).GitHubToken.ShouldBeNull();
    }

    [TestCase("http://example.com")]
    [TestCase("example.com/path")]
    [TestCase("example.com:8080")]
    [TestCase("bad host")]
    [TestCase("-bad.example.com")]
    public async Task Integrations_WhenAllowListInvalidHost_ShouldBeValidationOnThatField(string line)
    {
        using var response = await Api.PutJsonAsync("api/instance/integrations", new { previewAllowHosts = new[] { "ok.example.com", line } });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("previewAllowHosts");
    }

    [TestCase("not a url")]
    [TestCase("ftp://gitlab.example.com")]
    [TestCase("https://user:pw@gitlab.example.com")]
    public async Task Integrations_WhenGitLabUrlInvalid_ShouldBeValidationOnThatField(string url)
    {
        using var response = await Api.PutJsonAsync("api/instance/integrations", new { gitLabUrl = url });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Field.ShouldBe("gitLabUrl");
    }

    [Test]
    public async Task Integrations_WhenGitLabTokenIsForAnHttpUrl_ShouldBeValidationAndNotEchoTheToken()
    {
        using var response = await Api.PutJsonAsync("api/instance/integrations", new { gitLabUrl = "http://gitlab.example.com", gitLabToken = "glpat-leaky999999" });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Field.ShouldBe("gitLabUrl");
        error.Error.ShouldNotContain("leaky");
    }

    [Test]
    public async Task Integrations_WhenGitLabTokenHasNoUrl_ShouldBeValidation()
    {
        using var response = await Api.PutJsonAsync("api/instance/integrations", new { gitLabToken = "glpat-leaky999999" });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Field.ShouldBe("gitLabToken");
        error.Error.ShouldNotContain("leaky");
    }

    [Test]
    public async Task Integrations_WhenTokenHasWhitespace_ShouldBeValidationAndNotEchoTheToken()
    {
        using var response = await Api.PutJsonAsync("api/instance/integrations", new { gitHubToken = "ghp_two words" });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Field.ShouldBe("gitHubToken");
        error.Error.ShouldNotContain("ghp_");
    }

    [Test]
    public async Task Integrations_WhenNotInstanceAdmin_ShouldReturnForbidden()
    {
        var mei = Api.ActingAs(TestUser.Mei);

        using var read = await mei.GetAsync("api/instance/integrations");
        using var write = await mei.PutJsonAsync("api/instance/integrations", new { gitHubToken = "ghp_nope00000000" });

        foreach (var response in new[] { read, write })
        {
            (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
        }
    }
}
