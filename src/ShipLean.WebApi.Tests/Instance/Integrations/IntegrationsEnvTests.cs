using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Instance.Integrations;

// One seeded scenario with the environment variables set and nothing saved: they are what is in force.
[TestFixture]
public class IntegrationsEnvTests : ApiFixture
{
    protected override IReadOnlyDictionary<string, string?> HostSettings => IntegrationsWire.Env;

    [Test]
    public async Task Integrations_WhenDbEmpty_ShouldFallBackToEnv()
    {
        var dto = await IntegrationsWire.GetAsync(Api);
        var resolved = await IntegrationsWire.ResolveAsync(Host);

        dto.GitHubTokenFromEnv.ShouldBeTrue();
        dto.GitHubTokenLastFour.ShouldBe("0001");
        dto.GitLabFromEnv.ShouldBeTrue();
        dto.GitLabUrl.ShouldBe("https://gitlab.env.example.com");
        dto.GitLabTokenLastFour.ShouldBe("0002");
        resolved.GitHubToken.ShouldBe("ghp_envsecret0001");
        resolved.GitLabUrl.ShouldBe("https://gitlab.env.example.com");
        resolved.GitLabToken.ShouldBe("glpat-envsecret0002");
        resolved.GitHubApiUrl.ShouldBe("https://api.github.com");
        resolved.PreviewAllowHosts.ShouldBeEmpty();
    }
}
