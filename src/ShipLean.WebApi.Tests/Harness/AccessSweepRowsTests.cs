using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Harness;

[TestFixture]
public class AccessSweepRowsTests
{
    [Test]
    public void All_WhenImplementationIsPrivateNested_ShouldSkipIt()
    {
        var templates = AccessSweepRows.All().Select(r => r.RouteTemplate);

        templates.ShouldNotContain("api/projects/{projectId}");
        AccessSweepRows.From([typeof(SampleRows)]).ShouldNotBeEmpty();
    }

    [Test]
    public void From_WhenGivenTypes_ShouldCollectOnlyConcreteImplementations()
    {
        var rows = AccessSweepRows.From([typeof(SampleRows), typeof(AbstractRows), typeof(string), typeof(IAccessSweepRows)]);

        rows.ShouldBe([new AccessSweepRow("GET", "api/projects/{projectId}", ProjectRole.Member)]);
    }

    private sealed class SampleRows : IAccessSweepRows
    {
        public IEnumerable<AccessSweepRow> Rows => [new("GET", "api/projects/{projectId}", ProjectRole.Member)];
    }

    private abstract class AbstractRows : IAccessSweepRows
    {
        public IEnumerable<AccessSweepRow> Rows => [new("GET", "never", ProjectRole.Admin)];
    }
}
