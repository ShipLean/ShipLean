using ShipLean.Application.Features.Usage;
using ShipLean.Contracts.Analytics;
using ShipLean.Contracts.Usage;

namespace ShipLean.UnitTest.Usage;

[TestFixture]
public class UsageEventMapperAppSurfaceTests
{
    [Test]
    public void UsageEventMapper_WhenApp_ShouldWriteApp()
    {
        var target = new AnalyticsTarget(AnalyticsProvider.Umami, new Uri("https://umami.example.com"), "site-key", "ship.example.com", false, "1.2.3");
        var usageEvent = new UsageEvent(
            UsageEventNames.DeviceSessionEnded, UsageSurface.App, UsageActor.ForUser(Guid.NewGuid()), null, 0, UsageOutcome.Ok, null, 5,
            "api:Test.Action", new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero), new Dictionary<string, object>());

        var mapped = UsageEventMapper.Build(usageEvent, target, null, null);

        mapped.Properties[AnalyticsProps.Surface].ShouldBe("app");
    }
}
