using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Harness;

[TestFixture]
public class AccessSweepRowsAllTests
{
    // Only this test assembly is loaded in this process, so All() must give exactly what From() gives over its types.
    // Private and internal nested types are skipped by All() (probes and doubles of other tests), so they're left out here too.
    [Test]
    public void All_WhenAssembliesAreScanned_ShouldMatchTheImplementationsInThisAssembly()
    {
        var expected = AccessSweepRows.From(
            typeof(AccessSweepRowsAllTests).Assembly.GetTypes().Where(t => !(t.IsNested && !t.IsNestedPublic)));

        AccessSweepRows.All().ShouldBe(expected);
    }
}
