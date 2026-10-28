using System.Net;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Instance.AgentConnections;

// Shared by the agent connection tests: the wire calls, and what the dispatch code would read.
internal static class AgentConnectionsWire
{
    public const string Route = "api/instance/agent-connections";

    public static async Task<(T Value, string Text)> ReadAsync<T>(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return (JsonSerializer.Deserialize<T>(text, JsonSerializerOptions.Web)!, text);
    }

    public static async Task<AgentConnectionIssuedDto> CreateAsync(ApiClient api, string name, string url = "https://relay.example.com/hook")
        => (await ReadAsync<AgentConnectionIssuedDto>(await api.PostJsonAsync(Route, new { name, receiverUrl = url }))).Value;

    public static async Task<AgentConnectionIdentity?> FindByTokenAsync(ShipLeanHost host, string token)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAgentConnections>().FindActiveByCallbackTokenHashAsync(IAgentConnections.HashCallbackToken(token));
    }

    public static async Task<AgentConnection?> GetAsync(ShipLeanHost host, Guid id)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAgentConnections>().GetAsync(id);
    }

    public static Task<string> StoredSecretAsync(NpgsqlConnection connection, Guid id)
        => connection.QuerySingleAsync<string>("SELECT signing_secret_protected FROM agent_connection WHERE id = @id", new { id });
}
