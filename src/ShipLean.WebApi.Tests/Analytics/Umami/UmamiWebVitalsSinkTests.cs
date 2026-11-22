using System.Net;
using System.Text.Json.Nodes;
using ShipLean.Application.Features.Analytics.Umami;
using ShipLean.Contracts.Analytics;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Analytics;

namespace ShipLean.WebApi.Tests.Analytics.Umami;

[TestFixture]
public class UmamiWebVitalsSinkTests
{
    private const string SiteKey = "7f3c1a52-9d4e-4b8a-a1c6-2e5f0b9d3c71";
    private const string UserId = "user:3f2c8a1e-5b7d-4c9a-8e10-6d4b2a9f7c35";

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false);
        }
    }

    private AnalyticsSinkStubHandler _handler = null!;
    private UmamiWebVitalsSink _sink = null!;
    private AnalyticsTarget _target = null!;

    [TearDown]
    public void DisposeHandler()
    {
        _handler.Dispose();
    }

    [SetUp]
    public void CreateSink()
    {
        _handler = new AnalyticsSinkStubHandler();
        _sink = new UmamiWebVitalsSink(new StubFactory(_handler));
        _target = new AnalyticsTarget(AnalyticsProvider.Umami, new Uri("https://umami.example.com/"), SiteKey, "prod.shiplean.app", false, "1.2.3", true);
    }

    private static WebVitalsSample Sample(string screen = "taskboard", double? lcp = 812.4, double? inp = 96, double? cls = 0.02, double? fcp = null, double? ttfb = null, string? userAgent = "Mozilla/5.0 (X11; Linux x86_64)", string? screenSize = "1920x1080")
    {
        return new WebVitalsSample(screen, WebVitalsNavigation.Soft, lcp, inp, cls, fcp, ttfb, userAgent, screenSize, UserId, TestDates.Today);
    }

    [Test]
    public async Task Send_WhenAllMetricsPresent_ShouldPostAPerformanceItem()
    {
        await _sink.SendAsync([Sample(fcp: 400.5, ttfb: 120)], _target, CancellationToken.None);

        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Url.ToString().ShouldBe("https://umami.example.com/api/batch");
        request.Headers["User-Agent"].ShouldBe("ShipLean/1.2.3");
        var expected = JsonNode.Parse("""
            [
              {"type":"performance","payload":{"website":"7f3c1a52-9d4e-4b8a-a1c6-2e5f0b9d3c71","hostname":"prod.shiplean.app","url":"/web/taskboard","title":"taskboard","language":"","screen":"1920x1080","id":"user:3f2c8a1e-5b7d-4c9a-8e10-6d4b2a9f7c35","userAgent":"Mozilla/5.0 (X11; Linux x86_64)","lcp":812.4,"inp":96,"cls":0.02,"fcp":400.5,"ttfb":120}}
            ]
            """)!;
        JsonNode.DeepEquals(JsonNode.Parse(request.Body), expected).ShouldBeTrue(request.Body);
    }

    [Test]
    public async Task Send_WhenMetricsAreNull_ShouldOmitTheKeys()
    {
        await _sink.SendAsync([Sample(lcp: null, inp: null, cls: 0.02, userAgent: null, screenSize: null)], _target, CancellationToken.None);

        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Body.ShouldNotContain("null");
        var payload = JsonNode.Parse(request.Body)![0]!["payload"]!.AsObject();
        payload.Select(p => p.Key).ShouldBe(["website", "hostname", "url", "title", "language", "id", "cls"]);
    }

    [Test]
    public async Task Send_WhenAnySample_ShouldNotSendAnIp()
    {
        await _sink.SendAsync([Sample(fcp: 1, ttfb: 2)], _target, CancellationToken.None);

        var payload = JsonNode.Parse(_handler.Requests.ShouldHaveSingleItem().Body)![0]!["payload"]!.AsObject();
        payload.ContainsKey("ip").ShouldBeFalse();
        _handler.Requests[0].Body.ShouldNotContain("\"ip\"");
    }

    [Test]
    public async Task Send_WhenInstanceHostIsEmpty_ShouldStillSend()
    {
        _target = _target with { InstanceHost = string.Empty };

        await _sink.SendAsync([Sample()], _target, CancellationToken.None);

        var payload = JsonNode.Parse(_handler.Requests.ShouldHaveSingleItem().Body)![0]!["payload"]!;
        payload["hostname"]!.GetValue<string>().ShouldBe(string.Empty);
        payload["url"]!.GetValue<string>().ShouldBe("/web/taskboard");
    }

    [Test]
    public async Task Send_WhenOver500Samples_ShouldSplitRequests()
    {
        var batch = Enumerable.Range(0, 501).Select(_ => Sample()).ToList();

        await _sink.SendAsync(batch, _target, CancellationToken.None);

        _handler.Requests.Select(r => JsonNode.Parse(r.Body)!.AsArray().Count).ShouldBe([500, 1]);
    }

    [Test]
    public async Task Send_WhenUmamiReportsItemErrors_ShouldThrow()
    {
        _handler.Respond(HttpStatusCode.OK, """{"size":1,"processed":0,"errors":1,"details":[{"message":"lcp too big"}]}""");

        var exception = await Should.ThrowAsync<AnalyticsSendException>(() => _sink.SendAsync([Sample()], _target, CancellationToken.None));

        exception.Message.ShouldNotContain(SiteKey);
    }

    [Test]
    public async Task Send_WhenServerAnswers500_ShouldThrowWithoutTheSiteKey()
    {
        _handler.Respond(HttpStatusCode.InternalServerError, $$"""{"error":"{{SiteKey}}"}""");

        var exception = await Should.ThrowAsync<AnalyticsSendException>(() => _sink.SendAsync([Sample()], _target, CancellationToken.None));

        exception.Message.ShouldNotContain(SiteKey);
        exception.Message.ShouldContain("500");
    }
}
