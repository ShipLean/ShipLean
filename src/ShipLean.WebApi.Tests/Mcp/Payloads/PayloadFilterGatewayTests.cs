using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Mcp.Transport;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Payloads;

// The payload filter behind the gateway email header: the caller is a person with no agent authorization, so the payload
// lookup passes a null authorization.
[TestFixture]
public class PayloadFilterGatewayTests : McpFixture
{
    private const string Header = "x-user-email";

    private string _featureKey = string.Empty;

    protected override bool WithProbeTools => false;

    protected override IReadOnlyDictionary<string, string?> HostSettings { get; } = new Dictionary<string, string?>
    {
        [McpGatewaySettings.EmailHeaderKey] = Header,
    };

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.RemoveAll<IToolPayloads>();
        services.AddSingleton<FakeToolPayloads>();
        services.AddSingleton<IToolPayloads>(sp => sp.GetRequiredService<FakeToolPayloads>());
    };

    private FakeToolPayloads Payloads => Host.Services.GetRequiredService<FakeToolPayloads>();

    [OneTimeSetUp]
    public async Task ArrangeFeature()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Gateway payload target").InsertAsync(connection);
        _featureKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature });
    }

    [Test]
    public async Task ToolCall_WhenGatewayCallerUsesPayload_ShouldLookUpWithoutAuthorization()
    {
        var payload = Payloads.AddField("attach_markdown", "content", "# From the gateway", SeedIds.Users.Joel, null);

        var reply = await CallToolAsync(
            null, "attach_markdown", $$"""{"feature":"{{_featureKey}}","name":"gateway.md","payload":"{{payload}}"}""",
            request => request.Headers.Add(Header, TestUser.Joel.Email));

        reply.IsToolError().ShouldBeFalse(reply.Body);
        Payloads.Lookups.ToArray().ShouldBe(new[] { new FakeToolPayloads.Lookup(payload, "attach_markdown", SeedIds.Users.Joel, null) });
    }
}
