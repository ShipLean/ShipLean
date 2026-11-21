using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Mcp;
using ShipLean.WebApi.Features.Prompts;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Prompts;

// C5 over the real /mcp: initialize is remembered on the authorization, and each call adds its originator and User-Agent.
[TestFixture]
public class ConnectedAgentTests : McpFixture
{
    private static readonly string OpenAiCliWord = "co" + "dex";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddTransient<IStartupFilter, ConnectedAgentProbe>();
    };

    private async Task<ConnectedAgent> ProbeAsync(string token, Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp-probe/agent");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.Clear();
        configure?.Invoke(request);
        using var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ConnectedAgent>(Json) ?? throw new InvalidOperationException("No agent.");
    }

    private async Task<string?> StoredNameAsync(string clientName)
    {
        await using var connection = await Host.Services.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<string?>(
            "SELECT a.mcp_client_name FROM oauth_authorization a JOIN oauth_client c ON c.client_id = a.client_id WHERE c.client_name = @clientName",
            new { clientName });
    }

    private async Task InitializeAsAsync(string token, string name, string version = "1.0")
    {
        await using var client = await McpTestClient.ConnectAsync(new Uri(Host.BaseUrl + "/mcp"), token, new Implementation { Name = name, Version = version });
        client.ShouldNotBeNull();
    }

    [Test]
    public async Task Initialize_WhenClientInfoNamed_ShouldBeRememberedForTheToken()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly, "Test Agent");

        await InitializeAsAsync(token, "claude-code");

        (await ProbeAsync(token)).ShouldBe(new ConnectedAgent(PromptAgents.ClaudeCode, AgentSignalSource.Initialize, "claude-code"));
    }

    [Test]
    public async Task Call_WhenOriginatorHeader_ShouldBeOpenAiCli()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var agent = await ProbeAsync(token, request => request.Headers.Add("originator", OpenAiCliWord + "_cli_rs"));

        agent.ShouldBe(new ConnectedAgent(PromptAgents.OpenAiCli, AgentSignalSource.Originator, OpenAiCliWord + "_cli_rs"));
    }

    [Test]
    public async Task Call_WhenOnlyUserAgent_ShouldUseItsProduct()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var agent = await ProbeAsync(token, request => request.Headers.TryAddWithoutValidation("User-Agent", "Cursor/0.50.1"));

        agent.ShouldBe(new ConnectedAgent(PromptAgents.Cursor, AgentSignalSource.UserAgent, "Cursor"));
    }

    [Test]
    public async Task Call_WhenOnlyOAuthName_ShouldUseIt()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly, "Claude Code");

        (await ProbeAsync(token)).ShouldBe(new ConnectedAgent(PromptAgents.ClaudeCode, AgentSignalSource.OAuthClient, "Claude Code"));
    }

    [Test]
    public async Task Call_WhenNoSignal_ShouldBeOther()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly, "x");

        // A token always has its OAuth client name, so the one way to no signal is a token the app cannot name: blank it in the store.
        await using (var connection = await Host.Services.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync())
        {
            await connection.ExecuteAsync("UPDATE oauth_client SET client_name = ' ' WHERE client_name = 'x'");
        }

        (await ProbeAsync(token)).ShouldBe(new ConnectedAgent(PromptAgents.Other, AgentSignalSource.None, null));
    }

    [Test]
    public void ConnectedAgent_WhenOffARequest_ShouldBeOtherAndNotThrow()
    {
        using var scope = Host.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IConnectedAgent>().Current.ShouldBe(new ConnectedAgent(PromptAgents.Other, AgentSignalSource.None, null));
    }

    [Test]
    public async Task Initialize_WhenNameOver200_ShouldStoreItTruncated()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly, "Long Namer");

        await InitializeAsAsync(token, "  " + new string('a', 250));

        (await StoredNameAsync("Long Namer")).ShouldBe(new string('a', 200));
    }

    [Test]
    public async Task Initialize_WhenTokenRefreshed_ShouldStillBeRecognised()
    {
        var first = await TokenAsync(TestUser.Joel, ReadOnly, "Rotating Agent");
        await InitializeAsAsync(first, "claude-code");
        await using var scope = Host.Services.CreateAsyncScope();
        await using var connection = await scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync();
        var authorizationId = await connection.QuerySingleAsync<Guid>(
            "SELECT a.id FROM oauth_authorization a JOIN oauth_client c ON c.client_id = a.client_id WHERE c.client_name = 'Rotating Agent'");
        var baseUrl = await connection.QuerySingleOrDefaultAsync<string>("SELECT base_url FROM instance_settings");

        var second = (await scope.ServiceProvider.GetRequiredService<IAgentTokenIssuer>()
            .IssueAsync(authorizationId, AgentUrls.Resource(baseUrl, Host.BaseUrl))).AccessToken;

        second.ShouldNotBe(first);
        (await ProbeAsync(second)).ShouldBe(new ConnectedAgent(PromptAgents.ClaudeCode, AgentSignalSource.Initialize, "claude-code"));
    }

    [Test]
    public async Task McpTestClient_WhenClientInfoGiven_ShouldSendIt()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly, "Sender");

        await InitializeAsAsync(token, "cursor", "9.9");

        (await StoredNameAsync("Sender")).ShouldBe("cursor");
    }
}
