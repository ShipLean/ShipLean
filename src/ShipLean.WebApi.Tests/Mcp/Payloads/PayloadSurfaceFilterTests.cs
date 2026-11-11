using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Payloads;

// The payload filter rests on one SDK behaviour: a call-tool filter that changes the call's arguments changes what the
// tool's parameters bind to. This fixture adds one extra filter (test code only) that swaps attach_markdown's content.
[TestFixture]
public class PayloadSurfaceFilterTests : McpFixture
{
    private const string Replaced = "# Replaced by the filter";

    private string _featureKey = string.Empty;

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddMcpServer().WithRequestFilters(filters => filters.AddCallToolFilter(next => (request, cancellationToken) =>
        {
            if (request.Params is { Name: "attach_markdown", Arguments: { } arguments })
            {
                request.Params.Arguments = new Dictionary<string, JsonElement>(arguments) { ["content"] = JsonSerializer.SerializeToElement(Replaced) };
            }
            return next(request, cancellationToken);
        }));
    };

    [OneTimeSetUp]
    public async Task ArrangeFeature()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Filter target").InsertAsync(connection);
        _featureKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature });
    }

    [Test]
    public async Task CallToolFilter_WhenArgumentsChanged_ShouldBindChangedValues()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "attach_markdown", $$"""{"feature":"{{_featureKey}}","name":"swap.md","content":"# Original"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        await using var connection = await OpenDatabaseAsync();
        var stored = await connection.QuerySingleAsync<string?>(
            "SELECT search_text FROM attachment WHERE id = @id",
            new { id = reply.Structured().GetProperty("attachment_id").GetGuid() });
        stored.ShouldBe(Replaced);
    }
}
