using System.Net;
using Dapper;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Agents.Addresses;

// IAgentAddresses is exercised in a request, through the endpoint that publishes its resource.
[TestFixture]
public class McpUrlApiTests : ApiFixture
{
    private async Task<string> McpUrlWithBaseUrlAsync(string baseUrl)
    {
        string? original;
        await using (var connection = await OpenDatabaseAsync())
        {
            original = await connection.QuerySingleAsync<string>("SELECT base_url FROM instance_settings");
            await connection.ExecuteAsync("UPDATE instance_settings SET base_url = @baseUrl", new { baseUrl });
        }
        try
        {
            return (await Api.GetJsonAsync<McpUrlDto>("api/agents/mcp-url")).McpUrl;
        }
        finally
        {
            await using var connection = await OpenDatabaseAsync();
            await connection.ExecuteAsync("UPDATE instance_settings SET base_url = @original", new { original });
        }
    }

    [Test]
    public async Task McpUrl_WhenSignedIn_ShouldReturnResource()
    {
        var dto = await Api.ActingAs(TestUser.Mei).GetJsonAsync<McpUrlDto>("api/agents/mcp-url");

        dto.McpUrl.ShouldEndWith("/mcp");
    }

    [Test]
    public async Task McpUrl_WhenAnonymous_ShouldAnswer401()
    {
        using var response = await Api.ActingAs(TestUser.Anonymous).GetAsync("api/agents/mcp-url");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.Unauthorized);
    }

    [Test]
    public async Task AgentAddresses_WhenBaseUrlSet_ShouldUseIt()
    {
        (await McpUrlWithBaseUrlAsync("https://ship.example.test/")).ShouldBe("https://ship.example.test/mcp");
    }

    [Test]
    public async Task AgentAddresses_WhenBaseUrlUnset_ShouldUseRequestOrigin()
    {
        (await McpUrlWithBaseUrlAsync("")).ShouldBe(Host.BaseUrl.TrimEnd('/') + "/mcp");
    }
}
