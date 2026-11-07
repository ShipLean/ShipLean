using ShipLean.Contracts.Usage;

namespace ShipLean.UnitTest.Usage;

// Pins the extra property catalogue: a new key is a contract change (docs/analytics.md), so this list changes with it.
[TestFixture]
public class UsagePropsTests
{
    [Test]
    public void All_ShouldBeTheExtraPropertyCatalogue()
    {
        UsageProps.All.Order(StringComparer.Ordinal).ShouldBe(
            ["agent_name", "board", "count", "provider", "screen", "viewport"]);
    }
}
