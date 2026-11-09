using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Features.Mcp.Payloads;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Payloads;

// The `input` property on the usage events of the six tools that take a payload.
[TestFixture]
public class PayloadUsageTests : McpFixture
{
    private const string MarkedTitle = "Via payload";

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    protected override bool WithProbeTools => false;

    // Stands in for the payload filter (not built when this was written): marks the call as having used a payload, inside
    // ToolUsageFilter, when the arguments carry MarkedTitle.
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddHttpContextAccessor();
        services.AddMcpServer().WithRequestFilters(filters => filters.AddCallToolFilter(next => (request, cancellationToken) =>
        {
            if (JsonSerializer.Serialize(request.Params?.Arguments).Contains(MarkedTitle, StringComparison.Ordinal))
            {
                request.Services?.GetService<IHttpContextAccessor>()?.HttpContext?.Items[McpPayloadTargets.UsedItem] = true;
            }
            return next(request, cancellationToken);
        }));
    };

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
    public async Task ToolUsage_WhenUsedItemSet_ShouldTagInputPayload()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        await CallToolAsync(token, "capture_plan", $$$"""{"project":"SHIP","feature":{"title":"{{{MarkedTitle}}}","goal":"Tag it."}}""");

        var tracked = await Usage.WaitForAsync(e => e.Source == "tool:capture_plan", Wait);
        tracked.Properties[UsageProps.Input].ShouldBe("payload");
    }

    [Test]
    public async Task ToolUsage_WhenToolTakesNoPayload_ShouldNotTagInput()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        await CallToolAsync(token, "list_projects");

        var tracked = await Usage.WaitForAsync(e => e.Source == "tool:list_projects", Wait);
        tracked.Properties.ShouldNotContainKey(UsageProps.Input);
    }
}
