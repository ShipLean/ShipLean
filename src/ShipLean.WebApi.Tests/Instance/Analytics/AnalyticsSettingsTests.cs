using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Analytics;
using ShipLean.Contracts.Analytics;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Analytics;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Instance.Analytics;

[TestFixture]
public class AnalyticsSettingsTests : ApiFixture
{
    private const string UmamiKey = "7f3c1a52-9d4e-4b8a-a1c6-2e5f0b9d3c71";
    private const string PostHogKey = "phc_abcdefghijklmnop";

    private readonly RecordingAnalyticsSink _umami = new(AnalyticsProvider.Umami);
    private readonly RecordingAnalyticsSink _postHog = new(AnalyticsProvider.PostHog);

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IAnalyticsSink>();
        services.AddSingleton<IAnalyticsSink>(_umami);
        services.AddSingleton<IAnalyticsSink>(_postHog);
    };

    [SetUp]
    public async Task ResetToNone()
    {
        using var response = await PutAsync("none", null, null, false);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private Task<HttpResponseMessage> PutAsync(string provider, string? host, string? siteKey, bool sendNames)
        => Api.PutJsonAsync("api/instance/analytics", new { provider, host, siteKey, sendNames });

    private async Task<JsonNode> GetStoredAsync()
    {
        using var response = await Api.GetAsync("api/instance/analytics");
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    [Test]
    public async Task Put_WhenUmamiValid_ShouldStoreAndReadBack()
    {
        using var response = await PutAsync("umami", "https://umami.example.com", UmamiKey, true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var stored = await GetStoredAsync();
        stored["provider"]!.GetValue<string>().ShouldBe("umami");
        stored["host"]!.GetValue<string>().ShouldBe("https://umami.example.com");
        stored["siteKey"]!.GetValue<string>().ShouldBe(UmamiKey);
        stored["sendNames"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Test]
    public async Task Put_WhenPostHogValid_ShouldStoreAndReadBack()
    {
        using var response = await PutAsync("posthog", "https://eu.i.posthog.com/ingest", PostHogKey, false);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var stored = await GetStoredAsync();
        stored["provider"]!.GetValue<string>().ShouldBe("posthog");
        stored["host"]!.GetValue<string>().ShouldBe("https://eu.i.posthog.com/ingest");
        stored["siteKey"]!.GetValue<string>().ShouldBe(PostHogKey);
    }

    [Test]
    public async Task Put_WhenNone_ShouldClearHostAndKey()
    {
        (await PutAsync("umami", "https://umami.example.com", UmamiKey, false)).EnsureSuccessStatusCode();

        using var response = await PutAsync("none", "https://ignored.example.com", UmamiKey, false);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var stored = await GetStoredAsync();
        stored["provider"]!.GetValue<string>().ShouldBe("none");
        stored["host"].ShouldBeNull();
        stored["siteKey"].ShouldBeNull();
        await using var connection = await OpenDatabaseAsync();
        var row = await Dapper.SqlMapper.QuerySingleAsync<(string? Host, string? SiteKey)>(connection, "SELECT analytics_host AS Host, analytics_site_key AS SiteKey FROM instance_settings");
        row.ShouldBe(((string?)null, (string?)null));
    }

    [TestCase("umami", null, UmamiKey, "host")]
    [TestCase("umami", "ftp://umami.example.com", UmamiKey, "host")]
    [TestCase("umami", "https://umami.example.com?x=1", UmamiKey, "host")]
    [TestCase("umami", "https://umami.example.com/#top", UmamiKey, "host")]
    [TestCase("umami", "https://user:pw@umami.example.com", UmamiKey, "host")]
    [TestCase("umami", "https://umami.example.com", "not-a-guid", "siteKey")]
    [TestCase("posthog", "https://eu.i.posthog.com", "abc_123", "siteKey")]
    public async Task Put_WhenInvalid_ShouldBeValidationError(string provider, string? host, string siteKey, string field)
    {
        (await PutAsync("umami", "https://kept.example.com", UmamiKey, false)).EnsureSuccessStatusCode();

        using var response = await PutAsync(provider, host, siteKey, false);

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe(field);
        var stored = await GetStoredAsync();
        stored["host"]!.GetValue<string>().ShouldBe("https://kept.example.com");
        stored["siteKey"]!.GetValue<string>().ShouldBe(UmamiKey);
    }

    [Test]
    public async Task Put_WhenHostHasTrailingSlash_ShouldTrimIt()
    {
        using var response = await PutAsync("umami", "https://umami.example.com/analytics/", UmamiKey, false);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await GetStoredAsync())["host"]!.GetValue<string>().ShouldBe("https://umami.example.com/analytics");
    }

    [Test]
    public async Task Reader_WhenNone_ShouldReturnNull()
    {
        var target = await ReadTargetAsync();

        target.ShouldBeNull();
    }

    [Test]
    public async Task Reader_WhenUmami_ShouldCarryInstanceHostAndVersion()
    {
        (await PutAsync("umami", "https://umami.example.com", UmamiKey, true)).EnsureSuccessStatusCode();
        await using var connection = await OpenDatabaseAsync();
        var baseUrl = await Dapper.SqlMapper.QuerySingleAsync<string>(connection, "SELECT base_url FROM instance_settings");

        var target = await ReadTargetAsync();

        target.ShouldNotBeNull();
        target.Provider.ShouldBe(AnalyticsProvider.Umami);
        target.Host.ShouldBe(new Uri("https://umami.example.com"));
        target.SiteKey.ShouldBe(UmamiKey);
        target.SendNames.ShouldBeTrue();
        target.InstanceHost.ShouldBe(new Uri(baseUrl).Host);
        target.ShipLeanVersion.ShouldBe(ShipLeanVersion.Current);
        target.ShipLeanVersion.ShouldNotContain("+");
    }

    private async Task<AnalyticsTarget?> ReadTargetAsync()
    {
        var reader = Host.Services.GetRequiredService<IAnalyticsSettingsReader>();
        return await reader.GetTargetAsync(CancellationToken.None);
    }
}
