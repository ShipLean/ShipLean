using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Instance.Integrations;

// An environment GitLab token with no environment GitLab URL has no address to be paired with, so it is absent.
[TestFixture]
public class IntegrationsEnvTokenOnlyTests : ApiFixture
{
    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        ["SHIPLEAN_GITLAB_TOKEN"] = "glpat-orphan00001111",
    };

    [Test]
    public async Task Integrations_WhenEnvTokenHasNoEnvUrl_ShouldTreatItAsAbsent()
    {
        var dto = await IntegrationsWire.GetAsync(Api);
        var resolved = await IntegrationsWire.ResolveAsync(Host);

        dto.GitLabTokenLastFour.ShouldBeNull();
        dto.GitLabFromEnv.ShouldBeFalse();
        resolved.GitLabUrl.ShouldBeNull();
        resolved.GitLabToken.ShouldBeNull();
    }
}
