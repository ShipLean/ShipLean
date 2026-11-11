using ShipLean.Contracts.Usage;

namespace ShipLean.UnitTest.Usage;

// The phone screens' usage names (SHIP-916 N3, SHIP-926 K5): one dot each, as the catalogue's pattern requires.
[TestFixture]
public class ScreensUsageNamesTests
{
    [Test]
    public void UsageEventNames_WhenScreensNamesAdded_ShouldBeInAll()
    {
        string[] names =
        [
            UsageEventNames.RetroPointSupported, UsageEventNames.RetroPointUnsupported, UsageEventNames.FeatureDigestSeen,
            UsageEventNames.MentionSeen, UsageEventNames.MentionReplied,
        ];

        names.ShouldBe(["retro.point_supported", "retro.point_unsupported", "feature.digest_seen", "mention.seen", "mention.replied"]);
        names.ShouldAllBe(name => UsageEventNames.All.Contains(name));
    }
}
