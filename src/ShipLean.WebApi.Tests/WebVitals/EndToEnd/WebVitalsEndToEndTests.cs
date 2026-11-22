using Microsoft.Extensions.DependencyInjection;
using System.Net;
using ShipLean.Contracts.Analytics;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.WebVitals.EndToEnd;

// POST api/usage/web-vitals through the real queue and dispatcher to a recording sink.
[TestFixture]
public class WebVitalsEndToEndTests : ApiFixture
{
    private readonly WebVitalsEndToEnd _pipeline = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => _pipeline.Register;

    private static AnalyticsTarget Target(bool sendWebVitals)
        => new(AnalyticsProvider.Umami, new Uri("https://umami.example.com"), "site-key-0001", "ship.example.com", false, "1.0.0", sendWebVitals);

    [Test]
    public async Task Post_WhenSwitchOn_ShouldReachTheSink()
    {
        _pipeline.Settings.Target = Target(true);

        using var response = await Api.PostJsonAsync("api/usage/web-vitals", new
        {
            reports = new[] { new { screen = "taskboard", navigation = "soft", lcp = 812.4, inp = (double?)96, cls = (double?)0.02, fcp = (double?)null, ttfb = (double?)null, screenSize = "1920x1080" } },
        });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        await _pipeline.AdvanceUntilAsync(() => _pipeline.Sink.Calls.Count > 0);

        var call = _pipeline.Sink.Calls.Single();
        call.Target.Provider.ShouldBe(AnalyticsProvider.Umami);
        var sample = call.Batch.Single();
        sample.Screen.ShouldBe("taskboard");
        sample.Lcp.ShouldBe(812.4);
        sample.ScreenSize.ShouldBe("1920x1080");
    }
}
