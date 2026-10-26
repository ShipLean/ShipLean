using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Instance.Integrations;

// A GitLab token only goes to the URL it was saved with. The environment has its own URL and token.
[TestFixture]
public class IntegrationsPairingTests : ApiFixture
{
    protected override IReadOnlyDictionary<string, string?> HostSettings => IntegrationsWire.Env;

    [Test]
    [Order(1)]
    public async Task Integrations_WhenDbGitLabUrlHasNoDbToken_ShouldNeverResolveToTheEnvToken()
    {
        using var saved = await Api.PutJsonAsync("api/instance/integrations", new { gitLabUrl = "https://gitlab.db.example.com" });

        var dto = (await IntegrationsWire.ReadAsync(saved)).Dto;
        var resolved = await IntegrationsWire.ResolveAsync(Host);
        dto.GitLabFromEnv.ShouldBeFalse();
        dto.GitLabTokenLastFour.ShouldBeNull();
        resolved.GitLabUrl.ShouldBe("https://gitlab.db.example.com");
        resolved.GitLabToken.ShouldBeNull();
    }

    [Test]
    [Order(2)]
    public async Task Integrations_WhenDbGitLabUrlClearedAfterDbToken_ShouldNeverSendTheDbTokenToTheEnvUrl()
    {
        using var withToken = await Api.PutJsonAsync("api/instance/integrations", new { gitLabToken = "glpat-dbsecret7777" });
        (await IntegrationsWire.ResolveAsync(Host)).GitLabToken.ShouldBe("glpat-dbsecret7777");

        using var cleared = await Api.PutJsonAsync("api/instance/integrations", new { gitLabUrl = "" });

        var resolved = await IntegrationsWire.ResolveAsync(Host);
        (await IntegrationsWire.ReadAsync(cleared)).Dto.GitLabFromEnv.ShouldBeTrue();
        resolved.GitLabUrl.ShouldBe("https://gitlab.env.example.com");
        resolved.GitLabToken.ShouldBe("glpat-envsecret0002");
    }
}
