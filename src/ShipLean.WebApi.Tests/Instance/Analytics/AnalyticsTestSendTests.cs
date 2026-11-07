using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using ShipLean.Application.Features.Analytics;
using ShipLean.Contracts;
using ShipLean.Contracts.Analytics;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Analytics;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Instance.Analytics;

[TestFixture]
public class AnalyticsTestSendTests : ApiFixture
{
    private const string UmamiKey = "7f3c1a52-9d4e-4b8a-a1c6-2e5f0b9d3c71";

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
        _umami.FailWith(null);
        using var response = await Api.PutJsonAsync("api/instance/analytics", new { provider = "none", host = (string?)null, siteKey = (string?)null, sendNames = false });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private async Task<JsonNode> PostTestAsync()
    {
        using var response = await Api.PostJsonAsync("api/instance/analytics/test", new { });
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonNode.Parse(text)!;
    }

    private async Task UseUmamiAsync()
    {
        using var response = await Api.PutJsonAsync("api/instance/analytics", new { provider = "umami", host = "https://umami.example.com", siteKey = UmamiKey, sendNames = false });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task Test_WhenNone_ShouldSayAnalyticsIsOff()
    {
        var before = _umami.Calls.Count + _postHog.Calls.Count;

        var result = await PostTestAsync();

        result["ok"]!.GetValue<bool>().ShouldBeFalse();
        result["message"]!.GetValue<string>().ShouldBe("Analytics is off.");
        (_umami.Calls.Count + _postHog.Calls.Count).ShouldBe(before);
    }

    [Test]
    public async Task Test_WhenSinkSucceeds_ShouldBeOkAndSendOneAnalyticsTestEvent()
    {
        await UseUmamiAsync();
        var before = _umami.Calls.Count;

        var result = await PostTestAsync();

        result["ok"]!.GetValue<bool>().ShouldBeTrue();
        result["message"]!.GetValue<string>().ShouldBe("Sent a test event to Umami.");
        _umami.Calls.Count.ShouldBe(before + 1);
        var call = _umami.Calls[^1];
        call.Target.Provider.ShouldBe(AnalyticsProvider.Umami);
        call.Target.Host.ShouldBe(new Uri("https://umami.example.com"));
        call.Target.SiteKey.ShouldBe(UmamiKey);
        call.Batch.ShouldHaveSingleItem();
        var sent = call.Batch[0];
        sent.Name.ShouldBe("analytics.test");
        sent.DistinctId.ShouldBe($"user:{TestUser.Joel.Id}");
        sent.ActorKind.ShouldBe("user");
        sent.ProjectKey.ShouldBeNull();
        sent.Properties[AnalyticsProps.UserId].ShouldBe(TestUser.Joel.Id.ToString());
        sent.Properties[AnalyticsProps.InstanceHost].ShouldBe(call.Target.InstanceHost);
        sent.Properties.ContainsKey(AnalyticsProps.ProjectId).ShouldBeFalse();
        sent.Person.DistinctId.ShouldBe(sent.DistinctId);
        sent.Properties.Keys.ShouldBe(
            [
                AnalyticsProps.Surface, AnalyticsProps.ActorKind, AnalyticsProps.UserId, AnalyticsProps.Outcome,
                AnalyticsProps.ShipLeanVersion, AnalyticsProps.InstanceHost, AnalyticsProps.ProjectCount, AnalyticsProps.Source,
            ],
            ignoreOrder: true);
        sent.Properties[AnalyticsProps.ProjectCount].ShouldBe(0L);
        sent.Properties[AnalyticsProps.Source].ShouldBe("api:Analytics.Test");
        _postHog.Calls.ShouldBeEmpty();
    }

    [Test]
    public async Task Test_WhenSinkThrows_ShouldBeNotOkWithReason()
    {
        await UseUmamiAsync();
        _umami.FailWith("Umami answered 401.");

        var result = await PostTestAsync();

        result["ok"]!.GetValue<bool>().ShouldBeFalse();
        result["message"]!.GetValue<string>().ShouldBe("Umami answered 401.");
    }

    [Test]
    public async Task TestAsync_WhenSinkOutlastsTimeout_ShouldBeNotOkWithReason()
    {
        var clock = new FakeTimeProvider(TimeProvider.System.GetUtcNow());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sink = new WaitingSink(entered);
        var target = new AnalyticsTarget(AnalyticsProvider.Umami, new Uri("https://umami.example.com"), UmamiKey, "shiplean.example.com", false, "1.0.0");
        var settings = new AnalyticsSettings(
            Host.Services.GetRequiredService<NpgsqlDataSource>(), new FixedReader(target), [sink], new FixedUser(TestUser.Joel.Id!.Value), clock);

        var pending = settings.TestAsync(CancellationToken.None);
        await entered.Task;
        clock.Advance(TimeSpan.FromSeconds(10));
        var result = await pending;

        result.Ok.ShouldBeFalse();
        result.Message.ShouldStartWith("Umami didn't answer within 10 seconds: ");
        result.Message.Length.ShouldBeGreaterThan("Umami didn't answer within 10 seconds: ".Length);
    }

    private sealed class WaitingSink(TaskCompletionSource entered) : IAnalyticsSink
    {
        public AnalyticsProvider Provider => AnalyticsProvider.Umami;

        public async Task SendAsync(IReadOnlyList<AnalyticsEvent> batch, AnalyticsTarget target, CancellationToken cancellationToken)
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class FixedReader(AnalyticsTarget target) : IAnalyticsSettingsReader
    {
        public Task<AnalyticsTarget?> GetTargetAsync(CancellationToken cancellationToken) => Task.FromResult<AnalyticsTarget?>(target);
    }

    private sealed class FixedUser(Guid id) : ICurrentUser
    {
        public Guid Id => id;
    }
}
