using ShipLean.Contracts.Usage;

namespace ShipLean.UnitTest.Usage;

// The viewport header's values: an exact lowercase match, else no viewport at all (never a default).
[TestFixture]
public class UsageViewportsTests
{
    [TestCase("phone")]
    [TestCase("tablet")]
    [TestCase("desktop")]
    public void Parse_WhenKnown_ShouldReturnIt(string header)
    {
        UsageViewports.Parse(header).ShouldBe(header);
    }

    [TestCase("Phone")]
    [TestCase("DESKTOP")]
    [TestCase("wide")]
    [TestCase(" tablet")]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void Parse_WhenUnknownOrBlank_ShouldBeNull(string? header)
    {
        UsageViewports.Parse(header).ShouldBeNull();
    }
}
