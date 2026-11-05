using System.Net;
using System.Text.Json.Nodes;
using ShipLean.Application.Features.Analytics.Umami;
using ShipLean.Contracts.Analytics;
using ShipLean.TestSupport.Analytics;

namespace ShipLean.WebApi.Tests.Analytics.Umami;

[TestFixture]
public class UmamiSinkTests
{
    private const string SiteKey = "7f3c1a52-9d4e-4b8a-a1c6-2e5f0b9d3c71";

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false);
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("connection refused");
        }
    }

    private AnalyticsSinkStubHandler _handler = null!;
    private UmamiSink _sink = null!;
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
        _sink = new UmamiSink(new StubFactory(_handler));
        _target = new AnalyticsTarget(AnalyticsProvider.Umami, new Uri("https://umami.example.com/"), SiteKey, "prod.shiplean.app", false, "1.2.3");
    }

    private static AnalyticsEvent Event(string name = "project.created", string distinctId = "u-1", string? projectKey = "SHIP", IReadOnlyDictionary<string, object>? properties = null)
    {
        var props = properties ?? new Dictionary<string, object>
        {
            [AnalyticsProps.Surface] = "web",
            [AnalyticsProps.ActorKind] = "user",
            [AnalyticsProps.DurationMs] = 12L,
            [AnalyticsProps.AgentVerified] = true,
            ["ratio"] = 0.5,
        };
        var person = new AnalyticsPerson(distinctId, new Dictionary<string, object> { [AnalyticsProps.PersonKind] = "user", [AnalyticsProps.PersonUserId] = distinctId });
        return new AnalyticsEvent(name, distinctId, "user", projectKey, DateTimeOffset.Parse("2026-11-02T01:02:03Z", System.Globalization.CultureInfo.InvariantCulture), props, person);
    }

    private JsonArray LastBody()
    {
        return JsonNode.Parse(_handler.Requests[^1].Body)!.AsArray();
    }

    [Test]
    public async Task Send_WhenOneEvent_ShouldPostIdentifyThenEventToApiBatch()
    {
        await _sink.SendAsync([Event()], _target, CancellationToken.None);

        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Url.ToString().ShouldBe("https://umami.example.com/api/batch");
        var expected = JsonNode.Parse("""
            [
              {"type":"identify","payload":{"website":"7f3c1a52-9d4e-4b8a-a1c6-2e5f0b9d3c71","hostname":"prod.shiplean.app","id":"u-1","data":{"kind":"user","user_id":"u-1"}}},
              {"type":"event","payload":{"website":"7f3c1a52-9d4e-4b8a-a1c6-2e5f0b9d3c71","hostname":"prod.shiplean.app","url":"/p/SHIP/web/project/created","title":"project.created","name":"project.created","id":"u-1","tag":"user","language":"","screen":"","referrer":"","data":{"surface":"web","actor_kind":"user","duration_ms":12,"agent_verified":true,"ratio":0.5}}}
            ]
            """)!;
        JsonNode.DeepEquals(JsonNode.Parse(request.Body), expected).ShouldBeTrue(request.Body);
    }

    [Test]
    public async Task Send_WhenScreenViewed_ShouldSendCustomEventAndPageView()
    {
        var properties = new Dictionary<string, object> { [AnalyticsProps.Surface] = "web", ["screen"] = "board" };
        await _sink.SendAsync([Event("screen.viewed", properties: properties)], _target, CancellationToken.None);

        var body = LastBody();
        body.Count.ShouldBe(3);
        body[1]!["payload"]!["name"]!.GetValue<string>().ShouldBe("screen.viewed");
        var page = body[2]!["payload"]!;
        page["url"]!.GetValue<string>().ShouldBe("/p/SHIP/board");
        page["name"].ShouldBeNull();
        page["id"]!.GetValue<string>().ShouldBe("u-1");
        page["tag"]!.GetValue<string>().ShouldBe("user");
    }

    [Test]
    public async Task Send_WhenScreenViewsCrossBatchLimit_ShouldKeepRequestsWithinLimit()
    {
        var properties = new Dictionary<string, object> { ["screen"] = "board" };
        var batch = Enumerable.Range(0, 250).Select(_ => Event("screen.viewed", properties: properties)).ToList();
        await _sink.SendAsync(batch, _target, CancellationToken.None);

        _handler.Requests.Select(r => JsonNode.Parse(r.Body)!.AsArray().Count).ShouldBe([499, 2]);
    }

    [Test]
    public async Task Send_WhenSameActorTwice_ShouldIdentifyOnce()
    {
        await _sink.SendAsync([Event()], _target, CancellationToken.None);
        await _sink.SendAsync([Event(), Event("project.left")], _target, CancellationToken.None);

        var types = _handler.Requests.SelectMany(r => JsonNode.Parse(r.Body)!.AsArray()).Select(i => i!["type"]!.GetValue<string>()).ToList();
        types.ShouldBe(["identify", "event", "event", "event"]);
    }

    [Test]
    public async Task Send_WhenFirstAttemptFails_ShouldIdentifyAgainOnRetry()
    {
        _handler.Respond(HttpStatusCode.InternalServerError);
        await Should.ThrowAsync<AnalyticsSendException>(() => _sink.SendAsync([Event()], _target, CancellationToken.None));
        _handler.Respond(HttpStatusCode.OK);
        await _sink.SendAsync([Event()], _target, CancellationToken.None);

        LastBody()[0]!["type"]!.GetValue<string>().ShouldBe("identify");
    }

    [Test]
    public async Task Send_WhenOver500Items_ShouldSplitIntoRequestsOf500()
    {
        var batch = Enumerable.Range(0, 600).Select(i => Event(distinctId: "u-1")).ToList();
        await _sink.SendAsync(batch, _target, CancellationToken.None);

        _handler.Requests.Select(r => JsonNode.Parse(r.Body)!.AsArray().Count).ShouldBe([500, 101]);
    }

    [Test]
    public async Task Send_WhenNoProject_ShouldUseUnderscoreInPath()
    {
        await _sink.SendAsync([Event(projectKey: null)], _target, CancellationToken.None);

        LastBody()[1]!["payload"]!["url"]!.GetValue<string>().ShouldBe("/p/_/web/project/created");
    }

    [Test]
    public async Task Send_WhenOver50Properties_ShouldKeepStandardOnesAndTrim()
    {
        var props = new Dictionary<string, object>();
        for (var i = 0; i < 60; i++)
        {
            props[$"extra_{i:00}"] = (long)i;
        }
        props[AnalyticsProps.Surface] = "web";
        props[AnalyticsProps.InstanceHost] = "prod.shiplean.app";
        await _sink.SendAsync([Event(properties: props)], _target, CancellationToken.None);

        var data = LastBody()[1]!["payload"]!["data"]!.AsObject();
        data.Count.ShouldBe(50);
        data.ContainsKey(AnalyticsProps.Surface).ShouldBeTrue();
        data.ContainsKey(AnalyticsProps.InstanceHost).ShouldBeTrue();
        data.ContainsKey("extra_47").ShouldBeTrue();
        data.ContainsKey("extra_48").ShouldBeFalse();
    }

    [Test]
    public async Task Send_WhenStringOver500_ShouldCut()
    {
        var props = new Dictionary<string, object> { [AnalyticsProps.Outcome] = new string('x', 800), ["short"] = "ok" };
        await _sink.SendAsync([Event(properties: props)], _target, CancellationToken.None);

        var data = LastBody()[1]!["payload"]!["data"]!.AsObject();
        data[AnalyticsProps.Outcome]!.GetValue<string>().Length.ShouldBe(500);
        data["short"]!.GetValue<string>().ShouldBe("ok");
    }

    [Test]
    public async Task Send_When500_ShouldThrowAnalyticsSendException()
    {
        _handler.Respond(HttpStatusCode.InternalServerError, "boom");

        var exception = await Should.ThrowAsync<AnalyticsSendException>(() => _sink.SendAsync([Event()], _target, CancellationToken.None));

        exception.Message.ShouldContain("500");
        exception.Message.ShouldNotContain(SiteKey);
        exception.Message.ShouldNotContain("boom");
    }

    [Test]
    public async Task Send_WhenBatchReportsErrors_ShouldThrow()
    {
        _handler.Respond(HttpStatusCode.OK, """{"size":2,"processed":1,"errors":1,"details":[{"index":0,"response":{"message":"Website not found."}}]}""");

        var exception = await Should.ThrowAsync<AnalyticsSendException>(() => _sink.SendAsync([Event()], _target, CancellationToken.None));

        exception.Message.ShouldContain("1");
        exception.Message.ShouldNotContain(SiteKey);
        exception.Message.ShouldNotContain("Website not found");
    }

    [Test]
    public async Task Send_WhenHostUnreachable_ShouldThrowWithoutSiteKeyInMessage()
    {
        using var failing = new FailingHandler();
        var sink = new UmamiSink(new StubFactory(failing));

        var exception = await Should.ThrowAsync<AnalyticsSendException>(() => sink.SendAsync([Event()], _target, CancellationToken.None));

        exception.Message.ShouldContain("umami.example.com");
        exception.Message.ShouldNotContain(SiteKey);
    }

    [Test]
    public async Task Send_WhenBatchReportsNoErrors_ShouldNotThrow()
    {
        _handler.Respond(HttpStatusCode.OK, """{"size":2,"processed":2,"errors":0,"details":[]}""");

        await _sink.SendAsync([Event()], _target, CancellationToken.None);

        _handler.Requests.Count.ShouldBe(1);
    }

    [Test]
    public async Task Send_ShouldSetShipLeanUserAgent()
    {
        await _sink.SendAsync([Event()], _target, CancellationToken.None);

        var headers = _handler.Requests.ShouldHaveSingleItem().Headers;
        headers["User-Agent"].ShouldBe("ShipLean/1.2.3");
        headers["Content-Type"].ShouldStartWith("application/json");
    }
}
