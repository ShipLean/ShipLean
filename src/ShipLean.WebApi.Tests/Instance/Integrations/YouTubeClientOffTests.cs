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

        // Control: the same read, with a client saved, returns it (a hardcoded-null stub cannot). Cleared again for the next test.
        using var saved = await Api.PutJsonAsync("api/instance/integrations", new { youTubeClientId = "off-control.apps.example", youTubeClientSecret = "off-control-secret-4321" });
        saved.EnsureSuccessStatusCode();
        var resolved = await IntegrationsWire.ResolveYouTubeAsync(Host);
        using var cleared = await Api.PutJsonAsync("api/instance/integrations", new { youTubeClientId = "" });
        cleared.EnsureSuccessStatusCode();

        resolved.ShouldNotBeNull();
        resolved.ClientId.ShouldBe("off-control.apps.example");
        resolved.ClientSecret.ShouldBe("off-control-secret-4321");
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

        // Control: the YouTube path can turn on in this same host, and the sign-in client still never shows.
        using var saved = await Api.PutJsonAsync("api/instance/integrations", new { youTubeClientId = "on-control.apps.example", youTubeClientSecret = "on-control-secret-8765" });
        var (after, afterText) = await IntegrationsWire.ReadAsync(saved);
        var resolved = await IntegrationsWire.ResolveYouTubeAsync(Host);
        using var cleared = await Api.PutJsonAsync("api/instance/integrations", new { youTubeClientId = "" });
        cleared.EnsureSuccessStatusCode();

        after.YouTubeClientId.ShouldBe("on-control.apps.example");
        after.YouTubeClientSecretLastFour.ShouldBe("8765");
        afterText.ShouldNotContain("signin");
        resolved.ShouldNotBeNull();
        resolved.ClientId.ShouldBe("on-control.apps.example");
    }
}
