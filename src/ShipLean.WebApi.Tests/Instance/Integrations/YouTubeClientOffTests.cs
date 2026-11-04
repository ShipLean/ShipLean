using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Instance.Integrations;

// No YouTube client saved or in the environment. The sign-in client's SHIPLEAN_GOOGLE_* are set, as they would be on a
// real install, and must not stand in for it.
[TestFixture]
public class YouTubeClientOffTests : ApiFixture
{
    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        ["SHIPLEAN_GOOGLE_CLIENT_ID"] = "signin-client.apps.example",
        ["SHIPLEAN_GOOGLE_CLIENT_SECRET"] = "signin-secret-5555",
    };

    [Test]
    public async Task Get_WhenNothing_ShouldBeNull()
    {
        (await IntegrationsWire.ResolveYouTubeAsync(Host)).ShouldBeNull();
    }

    [Test]
    public async Task GoogleSignInSettings_ShouldNotBeRead()
    {
        var (dto, text) = await IntegrationsWire.ReadAsync(await Api.GetAsync("api/instance/integrations"));

        dto.YouTubeClientId.ShouldBeNull();
        dto.YouTubeClientSecretLastFour.ShouldBeNull();
        dto.YouTubeFromEnv.ShouldBeFalse();
        text.ShouldNotContain("signin");
        (await IntegrationsWire.ResolveYouTubeAsync(Host)).ShouldBeNull();
    }
}
