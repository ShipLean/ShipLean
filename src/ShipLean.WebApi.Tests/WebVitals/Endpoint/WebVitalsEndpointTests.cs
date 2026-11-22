using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Analytics;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.WebVitals;
using ShipLean.WebApi.Features.Usage;

namespace ShipLean.WebApi.Tests.WebVitals.Endpoint;

// GET api/usage/web-vitals/config and POST api/usage/web-vitals. The post checks and queues; it never looks at the switch.
[TestFixture]
public class WebVitalsEndpointTests : ApiFixture
{
    private const string UmamiKey = "7f3c1a52-9d4e-4b8a-a1c6-2e5f0b9d3c71";
    private const string ConfigPath = "api/usage/web-vitals/config";
    private const string PostPath = "api/usage/web-vitals";

    private RecordingWebVitals Recorder => (RecordingWebVitals)Host.Services.GetRequiredService<IWebVitalsQueue>();

    [SetUp]
    public async Task ResetToNone()
    {
        await SetAnalyticsAsync("none", null, null, false);
        Recorder.Clear();
    }

    private async Task SetAnalyticsAsync(string provider, string? host, string? siteKey, bool sendWebVitals)
    {
        using var response = await Api.PutJsonAsync("api/instance/analytics", new { provider, host, siteKey, sendNames = false, sendWebVitals });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static object Report(
        string screen = "taskboard",
        string navigation = "soft",
        double? lcp = 812.4,
        double? inp = 96,
        double? cls = 0.02,
        double? fcp = null,
        double? ttfb = null,
        string? screenSize = "1920x1080")
        => new { screen, navigation, lcp, inp, cls, fcp, ttfb, screenSize };

    private Task<HttpResponseMessage> PostAsync(params object[] reports) => Api.PostJsonAsync(PostPath, new { reports });

    private async Task<HttpResponseMessage> PostWithUserAgentAsync(string userAgent, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, PostPath);
        request.Headers.Add(ClientHeader.Name, ApiClient.ClientId);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent).ShouldBeTrue();
        request.Content = JsonContent.Create(body, options: JsonSerializerOptions.Web);
        return await Client.SendAsync(request);
    }

    private async Task<bool> ConfigEnabledAsync()
    {
        using var response = await Api.GetAsync(ConfigPath);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var config = await response.Content.ReadFromJsonAsync<WebVitalsConfigDto>(JsonSerializerOptions.Web);
        return config!.Enabled;
    }

    private static async Task ShouldBeValidationAsync(HttpResponseMessage response, string field)
    {
        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe(field);
    }

    [Test]
    public async Task Config_WhenSwitchOn_ShouldAnswerEnabled()
    {
        await SetAnalyticsAsync("umami", "https://umami.example.com", UmamiKey, true);

        (await ConfigEnabledAsync()).ShouldBeTrue();
    }

    [Test]
    public async Task Config_WhenSwitchOff_ShouldAnswerNotEnabled()
    {
        await SetAnalyticsAsync("umami", "https://umami.example.com", UmamiKey, false);

        (await ConfigEnabledAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task Config_WhenProviderNone_ShouldAnswerNotEnabled()
    {
        await SetAnalyticsAsync("umami", "https://umami.example.com", UmamiKey, true);
        (await ConfigEnabledAsync()).ShouldBeTrue();

        await SetAnalyticsAsync("none", null, null, true);

        (await ConfigEnabledAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task Config_WhenAnonymous_ShouldAnswer401()
    {
        using var response = await Api.ActingAs(TestUser.Anonymous).GetAsync(ConfigPath);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Post_WhenValid_ShouldQueueASampleWithTheUserAndUserAgent()
    {
        var userAgent = "Mozilla/5.0 " + new string('a', 600);
        var before = DateTimeOffset.UtcNow;

        using var response = await PostWithUserAgentAsync(userAgent, new { reports = new[] { Report(fcp: 120, ttfb: 40) } });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        var sample = Recorder.Samples.ShouldHaveSingleItem();
        sample.Screen.ShouldBe("taskboard");
        sample.Navigation.ShouldBe(WebVitalsNavigation.Soft);
        sample.Lcp.ShouldBe(812.4);
        sample.Inp.ShouldBe(96);
        sample.Cls.ShouldBe(0.02);
        sample.Fcp.ShouldBe(120);
        sample.Ttfb.ShouldBe(40);
        sample.UserAgent.ShouldBe(userAgent[..512]);
        sample.ScreenSize.ShouldBe("1920x1080");
        sample.DistinctId.ShouldBe(UsageActor.ForUser(SeedIds.Users.Joel).DistinctId);
        sample.OccurredAt.ShouldBeInRange(before.AddSeconds(-5), DateTimeOffset.UtcNow.AddSeconds(5));
    }

    [Test]
    public async Task Post_WhenSwitchOff_ShouldStillAnswer204()
    {
        await SetAnalyticsAsync("umami", "https://umami.example.com", UmamiKey, false);

        using var response = await PostAsync(Report(navigation: "hard", screenSize: null));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var sample = Recorder.Samples.ShouldHaveSingleItem();
        sample.Navigation.ShouldBe(WebVitalsNavigation.Hard);
        sample.ScreenSize.ShouldBeNull();
    }

    [Test]
    public async Task Post_WhenScreenUnknown_ShouldAnswer400WithField()
    {
        using var response = await PostAsync(Report(), Report(screen: "/web/taskboard"));

        await ShouldBeValidationAsync(response, "screen");
        Recorder.Samples.ShouldBeEmpty();
    }

    [Test]
    public async Task Post_WhenReportsEmpty_ShouldAnswer400WithField()
    {
        using var response = await PostAsync();

        await ShouldBeValidationAsync(response, "reports");
        Recorder.Samples.ShouldBeEmpty();
    }

    [Test]
    public async Task Post_WhenOver20Reports_ShouldAnswer400WithField()
    {
        using var accepted = await PostAsync([.. Enumerable.Range(0, 20).Select(_ => Report())]);
        accepted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Recorder.Clear();

        using var response = await PostAsync([.. Enumerable.Range(0, 21).Select(_ => Report())]);

        await ShouldBeValidationAsync(response, "reports");
        Recorder.Samples.ShouldBeEmpty();
    }

    [TestCase("1920*1080")]
    [TestCase("1920x")]
    [TestCase("123456x1")]
    [TestCase("")]
    public async Task Post_WhenScreenSizeMalformed_ShouldAnswer400WithField(string screenSize)
    {
        using var response = await PostAsync(Report(), Report(screenSize: screenSize));

        await ShouldBeValidationAsync(response, "screenSize");
        Recorder.Samples.ShouldBeEmpty();
    }

    [Test]
    public async Task Post_WhenAnonymous_ShouldAnswer401()
    {
        using var response = await Api.ActingAs(TestUser.Anonymous).PostJsonAsync(PostPath, new { reports = new[] { Report() } });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Recorder.Samples.ShouldBeEmpty();
    }

    [Test]
    public async Task Post_WhenBodyTooLarge_ShouldAnswer413()
    {
        var body = JsonSerializer.Serialize(new { reports = new[] { Report() }, padding = new string('x', UsageController.WebVitalsMaxBodyBytes) }, JsonSerializerOptions.Web);
        using var request = new HttpRequestMessage(HttpMethod.Post, PostPath);
        request.Headers.Add(ClientHeader.Name, ApiClient.ClientId);
        // Kestrel refuses the body by its Content-Length; asking to continue first gets the 413 before any of the body is sent.
        request.Headers.ExpectContinue = true;
        request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        using var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        Recorder.Samples.ShouldBeEmpty();
    }

    [Test]
    public async Task Post_WhenMetricOutOfRange_ShouldDropOnlyThatMetric()
    {
        using var response = await PostAsync(
            Report(lcp: 60_001, inp: 60_000, cls: 0.1, fcp: 0, ttfb: 10),
            Report(lcp: 812.4, inp: -1, cls: 100.5, fcp: 120, ttfb: null));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Recorder.Samples.Count.ShouldBe(2);
        var first = Recorder.Samples[0];
        first.Lcp.ShouldBeNull();
        first.Inp.ShouldBe(60_000);
        first.Cls.ShouldBe(0.1);
        first.Fcp.ShouldBe(0);
        first.Ttfb.ShouldBe(10);
        var second = Recorder.Samples[1];
        second.Lcp.ShouldBe(812.4);
        second.Inp.ShouldBeNull();
        second.Cls.ShouldBeNull();
        second.Fcp.ShouldBe(120);
        second.Ttfb.ShouldBeNull();
    }

    [Test]
    public async Task Post_WhenNoMetricLeft_ShouldQueueNothing()
    {
        using var response = await PostAsync(
            Report(lcp: 70_000, inp: null, cls: -0.5, fcp: null, ttfb: null),
            Report(lcp: null, inp: null, cls: null, fcp: null, ttfb: null),
            Report(screen: "profile", lcp: 500, inp: null, cls: null));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Recorder.Samples.ShouldHaveSingleItem().Screen.ShouldBe("profile");
    }
}
