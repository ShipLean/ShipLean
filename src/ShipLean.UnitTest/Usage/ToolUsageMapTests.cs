using System.Reflection;
using ModelContextProtocol.Server;
using ShipLean.Contracts.Usage;
using ShipLean.WebApi;
using ShipLean.WebApi.Features.Mcp.Usage;

namespace ShipLean.UnitTest.Usage;

[TestFixture]
public class ToolUsageMapTests
{
    private const BindingFlags Methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static string[] ToolNames()
        => [.. typeof(ApiMarker).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(Methods))
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
            .OfType<McpServerToolAttribute>()
            .Select(attribute => attribute.Name!)
            .Order(StringComparer.Ordinal)];

    [Test]
    public void EveryTool_ShouldBeMapped()
    {
        var tools = ToolNames();

        tools.ShouldNotBeEmpty();
        tools.Where(name => !ToolUsageMap.Events.ContainsKey(name)).ShouldBeEmpty();
    }

    [Test]
    public void EveryMappedName_ShouldBeInCatalogue()
    {
        ToolUsageMap.Events.Values.Where(name => !UsageEventNames.All.Contains(name)).ShouldBeEmpty();
    }

    [Test]
    public void EveryMapEntry_ShouldBeAToolThatExists()
    {
        var tools = ToolNames();

        ToolUsageMap.Events.Keys.Where(name => !tools.Contains(name)).ShouldBeEmpty();
    }
}
