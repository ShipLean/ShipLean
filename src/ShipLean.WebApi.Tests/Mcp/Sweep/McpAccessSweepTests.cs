using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Sweep;

// The real app's tools, with no probe tools: every one has a row, and every row is refused with not_found. Passes on the
// empty tool list (no tool exists yet) and starts to bite the moment one does.
[TestFixture]
public class McpAccessSweepTests : McpFixture
{
    protected override bool WithProbeTools => false;

    [Test]
    public void McpAccessSweep_WhenToolsRegistered_ShouldEachHaveARow()
    {
        var problems = McpSweep.RowProblems(McpSweep.ToolNames(Host.Services), McpSweep.AllRows());

        problems.ShouldBeEmpty();
    }

    [Test]
    public async Task McpAccessSweep_WhenCalledAsJoel_ShouldBeNotFound()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);

        var problems = await McpSweep.CallProblemsAsync((tool, arguments) => CallToolAsync(token, tool, arguments), McpSweep.AllRows());

        problems.ShouldBeEmpty();
    }
}
