using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Prompts;

namespace ShipLean.WebApi.Tests.Prompts;

// C8 (AC11): the real serving log, read back from prompt_serving.
[TestFixture]
public class PromptServingLogTests : ApiFixture
{
    private readonly ConnectedAgentStub _connected = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IConnectedAgent>();
        services.AddSingleton<IConnectedAgent>(_connected);
    };

    private async Task<ResolvedPrompt> ResolveAsync(PromptRequest request)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPromptResolver>().ResolveAsync(request);
    }

    private async Task<ServingRow> RowAsync(string itemKey)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<ServingRow>(
            """
            SELECT prompt, version, version_id AS versionId, built_in_hash AS builtInHash, agent, client_name AS clientName, client_source AS clientSource,
                   item_key AS itemKey, user_id AS userId
            FROM prompt_serving WHERE item_key = @itemKey
            """,
            new { itemKey });
    }

    [Test]
    public async Task Resolve_WhenServed_ShouldRecordPromptVersionAndAgent()
    {
        _connected.Agent = new ConnectedAgent(PromptAgents.ClaudeCode, AgentSignalSource.Initialize, "claude-code");
        var versionId = await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Brief).WithText("Custom brief.").InUse().InsertAsync(ConnectionString);

        var resolved = await ResolveAsync(new PromptRequest(SeedIds.Projects.Ship, PromptKey.Brief, ItemKey: "SHIP-20", UserId: SeedIds.Users.Joel));

        var row = await RowAsync("SHIP-20");
        (row.Prompt, row.Version, row.VersionId, row.BuiltInHash).ShouldBe(("brief", 1, versionId, resolved.BuiltInHash));
        (row.Agent, row.ClientName, row.ClientSource, row.UserId).ShouldBe((PromptAgents.ClaudeCode, "claude-code", "initialize", SeedIds.Users.Joel));
    }

    [Test]
    public async Task Resolve_WhenBuiltInServed_ShouldRecordANullVersion()
    {
        await ResolveAsync(new PromptRequest(SeedIds.Projects.Ship, PromptKey.Work, ItemKey: "SHIP-21"));

        var row = await RowAsync("SHIP-21");
        (row.Version, row.VersionId).ShouldBe((null, null));
    }

    [Test]
    public async Task ServingLog_WhenOffARequest_ShouldRecordANullUser()
    {
        Host.ActAs(null);

        await ResolveAsync(new PromptRequest(SeedIds.Projects.Ship, PromptKey.Guide, ItemKey: "SHIP-22"));
        await ResolveAsync(new PromptRequest(SeedIds.Projects.Ship, PromptKey.Guide, ItemKey: "SHIP-23", UserId: SeedIds.Users.Joel));

        (await RowAsync("SHIP-22")).UserId.ShouldBeNull();
        (await RowAsync("SHIP-23")).UserId.ShouldBe(SeedIds.Users.Joel);
    }

    private sealed class ConnectedAgentStub : IConnectedAgent
    {
        public volatile ConnectedAgent Agent = new(PromptAgents.Other, AgentSignalSource.None, null);

        public ConnectedAgent Current => Agent;
    }

    private sealed record ServingRow(string Prompt, int? Version, Guid? VersionId, string BuiltInHash, string Agent, string? ClientName, string? ClientSource, string? ItemKey, Guid? UserId);
}
