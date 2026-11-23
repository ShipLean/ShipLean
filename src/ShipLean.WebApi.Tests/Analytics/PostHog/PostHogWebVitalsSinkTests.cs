using System.Net;
using System.Text.Json;
using ShipLean.Application.Features.Analytics.PostHog;
using ShipLean.Contracts.Analytics;
using ShipLean.TestSupport.Analytics;

namespace ShipLean.WebApi.Tests.Analytics.PostHog;

[TestFixture]
public sealed class PostHogWebVitalsSinkTests
{
    private const string UserId = "user:3f2c8a1e-5b7d-4c9a-8e10-6d4b2a9f7c35";
    private const string ChromeWindows = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
    private static readonly DateTimeOffset At = new(2026, 11, 2, 3, 0, 0, TimeSpan.Zero);

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false);
        }
    }

    private AnalyticsSinkStubHandler _handler = null!;
    private PostHogWebVitalsSink _sink = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new AnalyticsSinkStubHandler();
        _sink = new PostHogWebVitalsSink(new StubFactory(_handler));
    }

    [TearDown]
    public void TearDown()
    {
        _handler.Dispose();
    }

    private static AnalyticsTarget Target(string instanceHost = "prod.example.com")
    {
        return new AnalyticsTarget(AnalyticsProvider.PostHog, new Uri("https://eu.i.posthog.com"), "phc_testkey", instanceHost, false, "1.2.3", true);
    }

    private static WebVitalsSample Sample(double? lcp = 812.4, double? inp = 96, double? cls = 0.02, double? fcp = null, double? ttfb = null, string? userAgent = ChromeWindows)
    {
        return new WebVitalsSample("taskboard", WebVitalsNavigation.Soft, lcp, inp, cls, fcp, ttfb, userAgent, "1920x1080", UserId, At);
    }

    private JsonElement Properties()
    {
        return JsonDocument.Parse(_handler.Requests[^1].Body).RootElement.GetProperty("batch")[0].GetProperty("properties").Clone();
    }

    [Test]
    public async Task Send_WhenAllMetricsPresent_ShouldPostAWebVitalsEvent()
    {
        await _sink.SendAsync([Sample()], Target(), CancellationToken.None);

        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Url.ToString().ShouldBe("https://eu.i.posthog.com/batch/");
        request.Headers["User-Agent"].ShouldBe("ShipLean/1.2.3");
        request.Body.ShouldBe(
            $$$"""{"api_key":"phc_testkey","historical_migration":false,"batch":[{"event":"$web_vitals","distinct_id":"{{{UserId}}}","timestamp":"2026-11-02T03:00:00.000Z","properties":{"$web_vitals_LCP_value":812.4,"$web_vitals_INP_value":96,"$web_vitals_CLS_value":0.02,"$pathname":"/web/taskboard","$host":"prod.example.com","$current_url":"https://prod.example.com/web/taskboard","$raw_user_agent":"{{{ChromeWindows}}}","$browser":"Chrome","$os":"Windows","$device_type":"Desktop","$lib":"shiplean","$process_person_profile":false,"screen":"taskboard","navigation":"soft"}}]}""");
    }

    [Test]
    public async Task Send_WhenOnlyTtfb_ShouldSendNothing()
    {
        await _sink.SendAsync([Sample(null, null, null, null, 120)], Target(), CancellationToken.None);

        _handler.Requests.ShouldBeEmpty();
    }

    [Test]
    public async Task Send_WhenOneSampleHasOnlyTtfb_ShouldSendTheOtherAndNoTtfb()
    {
        await _sink.SendAsync([Sample(null, null, null, null, 120), Sample(ttfb: 90)], Target(), CancellationToken.None);

        var body = _handler.Requests.ShouldHaveSingleItem().Body;
        JsonDocument.Parse(body).RootElement.GetProperty("batch").GetArrayLength().ShouldBe(1);
        body.ShouldNotContain("TTFB");
    }

    [Test]
    public async Task Send_WhenMetricsAreNull_ShouldOmitTheirProperties()
    {
        await _sink.SendAsync([Sample(lcp: null, inp: 96, cls: null, fcp: 410)], Target(), CancellationToken.None);

        var properties = Properties();
        properties.TryGetProperty("$web_vitals_LCP_value", out _).ShouldBeFalse();
        properties.TryGetProperty("$web_vitals_CLS_value", out _).ShouldBeFalse();
        properties.GetProperty("$web_vitals_INP_value").GetDouble().ShouldBe(96);
        properties.GetProperty("$web_vitals_FCP_value").GetDouble().ShouldBe(410);
    }

    [Test]
    public async Task Send_WhenInstanceHostIsEmpty_ShouldOmitHostAndCurrentUrl()
    {
        await _sink.SendAsync([Sample()], Target(instanceHost: ""), CancellationToken.None);

        var properties = Properties();
        properties.TryGetProperty("$host", out _).ShouldBeFalse();
        properties.TryGetProperty("$current_url", out _).ShouldBeFalse();
        properties.GetProperty("$pathname").GetString().ShouldBe("/web/taskboard");
    }

    [Test]
    public async Task Send_WhenNoUserAgent_ShouldOmitTheBrowserProperties()
    {
        await _sink.SendAsync([Sample(userAgent: null)], Target(), CancellationToken.None);

        var properties = Properties();
        foreach (var key in new[] { "$raw_user_agent", "$browser", "$os", "$device_type" })
        {
            properties.TryGetProperty(key, out _).ShouldBeFalse(key);
        }
        properties.GetProperty("$web_vitals_LCP_value").GetDouble().ShouldBe(812.4);
    }

    [Test]
    public async Task Send_WhenServerAnswers500_ShouldThrowWithoutTheKey()
    {
        _handler.Respond(HttpStatusCode.InternalServerError, "boom");

        var ex = await Should.ThrowAsync<AnalyticsSendException>(() => _sink.SendAsync([Sample()], Target(), CancellationToken.None));

        ex.Message.ShouldContain("500");
        ex.Message.ShouldNotContain("phc_testkey");
        _handler.Requests.ShouldHaveSingleItem();
    }
}
