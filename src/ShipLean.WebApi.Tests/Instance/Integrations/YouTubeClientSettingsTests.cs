using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Integrations;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Instance.Integrations;

// The YouTube OAuth client with SHIPLEAN_YOUTUBE_CLIENT_ID and _SECRET set and nothing saved, then saved over them, in order.
[TestFixture]
public class YouTubeClientSettingsTests : ApiFixture
{
    private const string EnvSecret = "yt-envsecret-0001";
    private const string SavedSecret = "yt-savedsecret-9876";

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        ["SHIPLEAN_YOUTUBE_CLIENT_ID"] = "env-client.apps.example",
        ["SHIPLEAN_YOUTUBE_CLIENT_SECRET"] = EnvSecret,
    };

    [Test]
    [Order(1)]
    public async Task Get_WhenEnv_ShouldUseIt()
    {
        var (dto, text) = await IntegrationsWire.ReadAsync(await Api.GetAsync("api/instance/integrations"));
        var resolved = await IntegrationsWire.ResolveYouTubeAsync(Host);

        resolved.ShouldNotBeNull();
        resolved.ClientId.ShouldBe("env-client.apps.example");
        resolved.ClientSecret.ShouldBe(EnvSecret);
        dto.YouTubeFromEnv.ShouldBeTrue();
        dto.YouTubeClientId.ShouldBe("env-client.apps.example");
        dto.YouTubeClientSecretLastFour.ShouldBe("0001");
        text.ShouldNotContain(EnvSecret);
    }

    [Test]
    [Order(2)]
    public async Task Put_WhenNotInstanceAdmin_ShouldBeForbidden()
    {
        var mei = Api.ActingAs(TestUser.Mei);

        using var write = await mei.PutJsonAsync("api/instance/integrations", new { youTubeClientId = "mei-client", youTubeClientSecret = "mei-secret-1234" });
        using var control = await Api.GetAsync("api/instance/integrations");

        (await ApiClient.ReadApiErrorAsync(write, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
        (await IntegrationsWire.ReadAsync(control)).Dto.YouTubeClientId.ShouldBe("env-client.apps.example");
        (await IntegrationsWire.ResolveYouTubeAsync(Host))!.ClientId.ShouldBe("env-client.apps.example");

        // Control: the same PUT body as the instance admin succeeds and persists, so the 403 is the refusal, not a dead PUT.
        using var admin = await Api.PutJsonAsync("api/instance/integrations", new { youTubeClientId = "mei-client", youTubeClientSecret = "mei-secret-1234" });
        var (saved, _) = await IntegrationsWire.ReadAsync(admin);
        saved.YouTubeClientId.ShouldBe("mei-client");
        saved.YouTubeClientSecretLastFour.ShouldBe("1234");
        (await IntegrationsWire.ResolveYouTubeAsync(Host))!.ClientId.ShouldBe("mei-client");
        (await IntegrationsWire.GetAsync(Api)).YouTubeClientId.ShouldBe("mei-client");
    }

    [Test]
    [Order(3)]
    public async Task Put_WhenNewIdHasNoSecret_ShouldNameTheSecret()
    {
        using var response = await Api.PutJsonAsync("api/instance/integrations", new { youTubeClientId = "half-client" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Field.ShouldBe("youTubeClientSecret");
    }

    [Test]
    [Order(4)]
    public async Task Get_WhenSaved_ShouldWinOverEnv()
    {
        using var saved = await Api.PutJsonAsync("api/instance/integrations", new { youTubeClientId = " saved-client.apps.example ", youTubeClientSecret = SavedSecret });
        var (dto, savedText) = await IntegrationsWire.ReadAsync(saved);
        var (read, readText) = await IntegrationsWire.ReadAsync(await Api.GetAsync("api/instance/integrations"));
        var resolved = await IntegrationsWire.ResolveYouTubeAsync(Host);

        resolved.ShouldNotBeNull();
        resolved.ClientId.ShouldBe("saved-client.apps.example");
        resolved.ClientSecret.ShouldBe(SavedSecret);
        dto.YouTubeFromEnv.ShouldBeFalse();
        dto.YouTubeClientId.ShouldBe("saved-client.apps.example");
        read.YouTubeClientSecretLastFour.ShouldBe("9876");
        foreach (var text in new[] { savedText, readText })
        {
            text.ShouldNotContain(SavedSecret);
            text.ShouldNotContain(EnvSecret);
            text.ShouldNotContain("savedsecret");
        }
    }

    [Test]
    [Order(5)]
    public async Task Save_ShouldProtectTheSecretAtRest()
    {
        await using var connection = await OpenDatabaseAsync();
        var stored = await connection.QuerySingleAsync<string>("SELECT youtube_client_secret FROM instance_settings");

        stored.ShouldNotContain("savedsecret");
        stored.ShouldNotBe(SavedSecret);
        Host.Services.GetRequiredService<IntegrationTokens>().Unprotect(stored).ShouldBe(SavedSecret);
    }

    [Test]
    [Order(6)]
    public async Task Read_ShouldNeverReturnTheSecret()
    {
        using var kept = await Api.PutJsonAsync("api/instance/integrations", new { gitHubToken = "ghp_unrelated1234" });
        var (dto, text) = await IntegrationsWire.ReadAsync(kept);

        dto.YouTubeClientSecretLastFour.ShouldBe("9876");
        text.ShouldNotContain(SavedSecret);
        text.ShouldNotContain("savedsecret");
        (await IntegrationsWire.ResolveYouTubeAsync(Host))!.ClientSecret.ShouldBe(SavedSecret);
    }
}
