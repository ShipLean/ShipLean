using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Instance.Integrations;

// Nothing saved and nothing in the environment: the GitHub API address defaults and the rest is left out.
[TestFixture]
public class IntegrationsUnsetTests : ApiFixture
{
    [Test]
    public async Task Integrations_WhenNothingIsSet_ShouldDefaultTheGitHubApiAndLeaveTheRestOut()
    {
        var resolved = await IntegrationsWire.ResolveAsync(Host);
        var dto = await IntegrationsWire.GetAsync(Api);

        resolved.GitHubToken.ShouldBeNull();
        resolved.GitHubApiUrl.ShouldBe("https://api.github.com");
        resolved.GitLabUrl.ShouldBeNull();
        resolved.GitLabToken.ShouldBeNull();
        resolved.PreviewAllowHosts.ShouldBeEmpty();
        dto.GitHubTokenFromEnv.ShouldBeFalse();
        dto.GitLabFromEnv.ShouldBeFalse();
    }
}
