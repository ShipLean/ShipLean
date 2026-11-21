using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Prompts.Sections;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Prompts;

namespace ShipLean.WebApi.Tests.Prompts;

// The resolver at wave 0.1: the built-in text, filtered for the agent, with a serving recorded when a project is named.
[TestFixture]
public class PromptResolverTests : ApiFixture
{
    private readonly RecordingPromptServingLog _log = new();
    private readonly ConnectedAgentStub _connected = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IPromptServingLog>();
        services.AddSingleton<IPromptServingLog>(_log);
        services.RemoveAll<IConnectedAgent>();
        services.AddSingleton<IConnectedAgent>(_connected);
    };

    [SetUp]
    public void ResetConnectedAgent() => _connected.Agent = new ConnectedAgent(PromptAgents.Other, AgentSignalSource.None, null);

    private async Task<ResolvedPrompt> ResolveAsync(PromptRequest request)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPromptResolver>().ResolveAsync(request);
    }

    [Test]
    public async Task Resolve_WhenNoProject_ShouldReturnTheBuiltInAndRecordNothing()
    {
        var before = _log.Servings.Count;

        var resolved = await ResolveAsync(new PromptRequest(null, PromptKey.Guide));

        var builtIns = Host.Services.GetRequiredService<IBuiltInPrompts>();
        resolved.Text.ShouldBe(AgentSections.Filter(builtIns.Text(PromptKey.Guide), PromptAgents.Other));
        _log.Servings.Count.ShouldBe(before);
    }

    [Test]
    public async Task Resolve_WhenNoCustom_ShouldReturnTheBuiltInWithNullVersion()
    {
        var resolved = await ResolveAsync(new PromptRequest(SeedIds.Projects.Ship, PromptKey.Brief, ItemKey: "SHIP-1"));

        var builtIns = Host.Services.GetRequiredService<IBuiltInPrompts>();
        resolved.Version.ShouldBeNull();
        resolved.Key.ShouldBe(PromptKey.Brief);
        resolved.BuiltInHash.ShouldBe(builtIns.Hash(PromptKey.Brief));
        var serving = _log.Servings.Single(s => s.ItemKey == "SHIP-1");
        (serving.ProjectId, serving.Key, serving.Version, serving.BuiltInHash).ShouldBe((SeedIds.Projects.Ship, PromptKey.Brief, null, builtIns.Hash(PromptKey.Brief)));
    }

    [Test]
    public async Task Resolve_WhenAgentGiven_ShouldUseIt()
    {
        var resolved = await ResolveAsync(new PromptRequest(SeedIds.Projects.Ship, PromptKey.Fix, PromptAgents.Cursor, "SHIP-2"));

        resolved.Agent.ShouldBe(PromptAgents.Cursor);
        _log.Servings.Single(s => s.ItemKey == "SHIP-2").Agent.ShouldBe(PromptAgents.Cursor);
    }

    [Test]
    public async Task Resolve_WhenNoRequest_ShouldRecordTheGivenUserAndNotThrow()
    {
        // No request and no acting user: reading ICurrentUser.Id here would throw.
        Host.ActAs(null);

        var resolved = await ResolveAsync(new PromptRequest(SeedIds.Projects.Ship, PromptKey.Retro, null, "SHIP-3", SeedIds.Users.Joel));

        resolved.Agent.ShouldBe(PromptAgents.Other);
        var serving = _log.Servings.Single(s => s.ItemKey == "SHIP-3");
        (serving.UserId, serving.Agent).ShouldBe((SeedIds.Users.Joel, PromptAgents.Other));
    }

    [Test]
    public async Task Resolve_WhenCustomInUse_ShouldReturnItsTextAndVersion()
    {
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Fix).WithText("First fix.").InsertAsync(ConnectionString);
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Fix).WithText("Second fix.").InUse().InsertAsync(ConnectionString);

        var resolved = await ResolveAsync(new PromptRequest(SeedIds.Projects.Ship, PromptKey.Fix, ItemKey: "SHIP-10"));

        (resolved.Text, resolved.Version).ShouldBe(("Second fix.", 2));
        _log.Servings.Single(s => s.ItemKey == "SHIP-10").Version.ShouldBe(2);
    }

    [Test]
    public async Task Resolve_WhenUsingBuiltIn_ShouldReturnTheBuiltIn()
    {
        // A custom exists but the project moved back to the built-in: the pointer is null.
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Report).WithText("Old report.").InsertAsync(ConnectionString);
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Report).WithKeptBuiltInHash("kept").InsertAsync(ConnectionString);

        var resolved = await ResolveAsync(new PromptRequest(SeedIds.Projects.Ship, PromptKey.Report, ItemKey: "SHIP-11"));

        var builtIns = Host.Services.GetRequiredService<IBuiltInPrompts>();
        resolved.Version.ShouldBeNull();
        resolved.Text.ShouldBe(AgentSections.Filter(builtIns.Text(PromptKey.Report), PromptAgents.Other));
    }

    [Test]
    public async Task Resolve_WhenNoProject_ShouldNotRecord()
    {
        var before = _log.Servings.Count;

        var resolved = await ResolveAsync(new PromptRequest(null, PromptKey.Lead, ItemKey: "SHIP-12"));

        resolved.Version.ShouldBeNull();
        _log.Servings.Count.ShouldBe(before);
    }

    [Test]
    public async Task Resolve_WhenConnectedAgentSignalled_ShouldRecordItsNameAndSource()
    {
        _connected.Agent = new ConnectedAgent(PromptAgents.Cursor, AgentSignalSource.Originator, "cursor-ide");
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Retro).WithText("Retro.\n<cursor>\nFor Cursor.\n</cursor>").InUse().InsertAsync(ConnectionString);

        var resolved = await ResolveAsync(new PromptRequest(SeedIds.Projects.Ship, PromptKey.Retro, ItemKey: "SHIP-13"));

        resolved.Agent.ShouldBe(PromptAgents.Cursor);
        resolved.Text.ShouldContain("For Cursor.");
        var serving = _log.Servings.Single(s => s.ItemKey == "SHIP-13");
        (serving.Agent, serving.ClientName, serving.ClientSource).ShouldBe((PromptAgents.Cursor, "cursor-ide", "originator"));
    }

    private sealed class ConnectedAgentStub : IConnectedAgent
    {
        public volatile ConnectedAgent Agent = new(PromptAgents.Other, AgentSignalSource.None, null);

        public ConnectedAgent Current => Agent;
    }
}
