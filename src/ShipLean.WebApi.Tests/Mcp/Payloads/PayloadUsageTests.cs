using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Features.Mcp.Usage;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Payloads;

// The `input` property on the usage events of the six tools that take a payload.
[TestFixture]
public class PayloadUsageTests : McpFixture
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    protected override bool WithProbeTools => false;

    private RecordingUsageEvents Usage => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearUsage() => Usage.Clear();

    [Test]
    public async Task ToolUsage_WhenCalledInline_ShouldTagInputInline()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        await CallToolAsync(token, "capture_plan", """{"project":"SHIP","feature":{"title":"Inline","goal":"Tag it."}}""");

        var tracked = await Usage.WaitForAsync(e => e.Source == "tool:capture_plan", Wait);
        tracked.Properties[UsageProps.Input].ShouldBe("inline");
    }

    [Test]
    public void ToolUsage_WhenUsedItemSet_ShouldTagInputPayload()
    {
        ToolUsageProps.For(toolTakesPayload: true, payloadUsed: true)[UsageProps.Input].ShouldBe("payload");
        ToolUsageProps.For(toolTakesPayload: true, payloadUsed: false)[UsageProps.Input].ShouldBe("inline");
    }

    [Test]
    public async Task ToolUsage_WhenToolTakesNoPayload_ShouldNotTagInput()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        await CallToolAsync(token, "list_projects");

        var tracked = await Usage.WaitForAsync(e => e.Source == "tool:list_projects", Wait);
        tracked.Properties.ShouldNotContainKey(UsageProps.Input);
        ToolUsageProps.For(toolTakesPayload: false, payloadUsed: true).ShouldBeEmpty();
    }
}
