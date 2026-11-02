using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ShipLean.Application.Features.Analytics.PostHog;
using ShipLean.Contracts.Analytics;
using ShipLean.TestSupport.Analytics;
using ShipLean.WebApi.Tests.Search.Ranking;

namespace ShipLean.WebApi.Tests.Analytics.PostHog;

[TestFixture]
public sealed class PostHogSinkTests
{
    private static readonly AnalyticsTarget Target = new(AnalyticsProvider.PostHog, new Uri("https://eu.i.posthog.com"), "phc_testkey", "ship.example", false, "1.2.3");
    private static readonly DateTimeOffset At = new(2026, 11, 2, 10, 30, 0, 123, TimeSpan.Zero);

    private AnalyticsSinkStubHandler _handler = null!;
    private PostHogSink _sink = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new AnalyticsSinkStubHandler();
        _sink = new PostHogSink(new HttpClient(_handler), NullLogger<PostHogSink>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _handler.Dispose();
    }

    private static AnalyticsEvent UserEvent(string name = "item.created", string? projectId = "p-1", string userName = "kind-user")
    {
        var props = new Dictionary<string, object> { [AnalyticsProps.Surface] = "web", [AnalyticsProps.ActorKind] = "user", [AnalyticsProps.UserId] = "u-1" };
        if (projectId is not null)
        {
            props[AnalyticsProps.ProjectId] = projectId;
        }
        var person = new AnalyticsPerson("u-1", new Dictionary<string, object> { [AnalyticsProps.PersonKind] = userName, [AnalyticsProps.PersonUserId] = "u-1" });
        return new AnalyticsEvent(name, "u-1", "user", "SHIP", At, props, person);
    }

    private JsonElement LastBody()
    {
        return JsonDocument.Parse(_handler.Requests[^1].Body).RootElement.Clone();
    }

    [Test]
    public async Task Send_WhenOneEvent_ShouldPostBatchWithApiKeyAndExactBody()
    {
        await _sink.SendAsync([UserEvent()], Target, CancellationToken.None);

        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Url.ToString().ShouldBe("https://eu.i.posthog.com/batch/");
        request.Headers["User-Agent"].ShouldBe("ShipLean/1.2.3");
        request.Body.ShouldBe(
            """{"api_key":"phc_testkey","historical_migration":false,"batch":[{"event":"item.created","distinct_id":"u-1","timestamp":"2026-11-02T10:30:00.123Z","properties":{"surface":"web","actor_kind":"user","user_id":"u-1","project_id":"p-1","$lib":"shiplean","$groups":{"project":"p-1"},"$set":{"kind":"kind-user","user_id":"u-1"}}}]}""");
    }

    [Test]
    public async Task Send_WhenProject_ShouldSetGroupsProject()
    {
        await _sink.SendAsync([UserEvent(projectId: "p-9")], Target, CancellationToken.None);

        LastBody().GetProperty("batch")[0].GetProperty("properties").GetProperty("$groups").GetProperty("project").GetString().ShouldBe("p-9");
    }

    [Test]
    public async Task Send_WhenNoProject_ShouldOmitGroups()
    {
        await _sink.SendAsync([UserEvent(projectId: null)], Target, CancellationToken.None);

        LastBody().GetProperty("batch")[0].GetProperty("properties").TryGetProperty("$groups", out _).ShouldBeFalse();
    }

    [Test]
    public async Task Send_WhenSameActorTwice_ShouldSetPersonOnce()
    {
        await _sink.SendAsync([UserEvent("a.one"), UserEvent("a.two")], Target, CancellationToken.None);
        await _sink.SendAsync([UserEvent("a.three")], Target, CancellationToken.None);

        var first = JsonDocument.Parse(_handler.Requests[0].Body).RootElement.GetProperty("batch");
        first[0].GetProperty("properties").TryGetProperty("$set", out _).ShouldBeTrue();
        first[1].GetProperty("properties").TryGetProperty("$set", out _).ShouldBeFalse();
        LastBody().GetProperty("batch")[0].GetProperty("properties").TryGetProperty("$set", out _).ShouldBeFalse();
    }

    [Test]
    public async Task Send_WhenPersonChanges_ShouldSetAgain()
    {
        await _sink.SendAsync([UserEvent(userName: "one")], Target, CancellationToken.None);
        await _sink.SendAsync([UserEvent(userName: "two")], Target, CancellationToken.None);

        LastBody().GetProperty("batch")[0].GetProperty("properties").GetProperty("$set").GetProperty("kind").GetString().ShouldBe("two");
    }

    [Test]
    public async Task Send_WhenFirstSendFails_ShouldSetPersonOnRetry()
    {
        _handler.Respond(HttpStatusCode.InternalServerError);
        await Should.ThrowAsync<AnalyticsSendException>(() => _sink.SendAsync([UserEvent()], Target, CancellationToken.None));
        _handler.Respond(HttpStatusCode.OK);

        await _sink.SendAsync([UserEvent()], Target, CancellationToken.None);

        LastBody().GetProperty("batch")[0].GetProperty("properties").TryGetProperty("$set", out _).ShouldBeTrue();
    }

    [Test]
    public async Task Send_WhenAgent_ShouldUseAgentDistinctIdAndAgentProperties()
    {
        var props = new Dictionary<string, object>
        {
            [AnalyticsProps.ActorKind] = "agent", [AnalyticsProps.UserId] = "u-1", [AnalyticsProps.AgentId] = "ag-1", [AnalyticsProps.AgentVerified] = true,
        };
        var person = new AnalyticsPerson("agent:ag-1", new Dictionary<string, object>
        {
            [AnalyticsProps.PersonKind] = "agent", [AnalyticsProps.PersonAgentName] = "Claude Code", [AnalyticsProps.PersonAgentVerified] = true,
        });

        await _sink.SendAsync([new AnalyticsEvent("tool.called", "agent:ag-1", "agent", null, At, props, person)], Target, CancellationToken.None);

        var item = LastBody().GetProperty("batch")[0];
        item.GetProperty("distinct_id").GetString().ShouldBe("agent:ag-1");
        item.GetProperty("properties").GetProperty("agent_verified").GetBoolean().ShouldBeTrue();
        item.GetProperty("properties").GetProperty("$set").GetProperty("agent_name").GetString().ShouldBe("Claude Code");
    }

    [Test]
    public async Task Send_When400_ShouldThrow()
    {
        _handler.Respond(HttpStatusCode.BadRequest, """{"error":"bad"}""");

        var ex = await Should.ThrowAsync<AnalyticsSendException>(() => _sink.SendAsync([UserEvent()], Target, CancellationToken.None));

        ex.Message.ShouldContain("400");
        ex.Message.ShouldNotContain("phc_testkey");
    }

    [Test]
    public async Task Send_WhenQuotaLimitedTwice_ShouldLogOneWarning()
    {
        CapturingLogger<PostHogSink> logger = new();
        using var client = new HttpClient(_handler);
        var quotaSink = new PostHogSink(client, logger);
        _handler.Respond(HttpStatusCode.OK, """{"status":1,"quota_limited":["events"]}""");

        await quotaSink.SendAsync([UserEvent()], Target, CancellationToken.None);
        await quotaSink.SendAsync([UserEvent()], Target, CancellationToken.None);

        _handler.Requests.Count.ShouldBe(2);
        logger.Lines.Count(l => l.Level == LogLevel.Warning).ShouldBe(1);
    }

    [Test]
    public async Task Send_WhenNotQuotaLimited_ShouldLogNoWarning()
    {
        CapturingLogger<PostHogSink> logger = new();
        using var client = new HttpClient(_handler);
        var plainSink = new PostHogSink(client, logger);
        _handler.Respond(HttpStatusCode.OK, """{"status":1}""");

        await plainSink.SendAsync([UserEvent()], Target, CancellationToken.None);

        _handler.Requests.Count.ShouldBe(1);
        logger.Lines.Count(l => l.Level == LogLevel.Warning).ShouldBe(0);
    }

    [Test]
    public async Task Send_WhenAnyEvent_ShouldNotSendProcessPersonProfile()
    {
        await _sink.SendAsync([UserEvent()], Target, CancellationToken.None);

        _handler.Requests[0].Body.ShouldNotContain("$process_person_profile");
    }
}
