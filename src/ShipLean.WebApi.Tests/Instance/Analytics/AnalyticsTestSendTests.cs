using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
}
