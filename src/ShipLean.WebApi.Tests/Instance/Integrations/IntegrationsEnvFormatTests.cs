using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Instance.Integrations;

// The environment values are trimmed, blank counts as unset, and the GitHub API address can be set (it is not saved).
[TestFixture]
public class IntegrationsEnvFormatTests : ApiFixture
{
    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        ["SHIPLEAN_GITHUB_TOKEN"] = " ghp_secret ",
        ["SHIPLEAN_GITHUB_API_URL"] = "https://ghe.example.com/api/v3",
        ["SHIPLEAN_GITLAB_URL"] = "https://gitlab.example.com",
        ["SHIPLEAN_GITLAB_TOKEN"] = "  ",
    };

    [Test]
    public async Task Integrations_WhenEnvironmentVariablesAreSet_ShouldReadThemAndTreatBlankAsUnset()
    {
        var resolved = await IntegrationsWire.ResolveAsync(Host);

        resolved.GitHubToken.ShouldBe("ghp_secret");
        resolved.GitHubApiUrl.ShouldBe("https://ghe.example.com/api/v3");
        resolved.GitLabUrl.ShouldBe("https://gitlab.example.com");
        resolved.GitLabToken.ShouldBeNull();
    }
}
