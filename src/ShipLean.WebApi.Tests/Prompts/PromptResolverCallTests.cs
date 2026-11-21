using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Plans;
using ShipLean.WebApi.Tests.Demos;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Prompts;

// Each place an agent gets a customizable prompt asks the resolver, with the project, key and item the call is about.
[TestFixture]
public class PromptResolverCallTests : McpFixture
{
    private readonly ResolverCalls _calls = new();
    private readonly SettableConnectedAgent _agent = new();
    private readonly UploadUrlPrompt _demoPrompt = new();
    private PlanSeedKeys _keys = null!;

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddSingleton(_calls);
        services.RemoveAll<IConnectedAgent>();
        services.AddSingleton<IConnectedAgent>(_agent);
        services.RemoveAll<IPromptResolver>();
        services.AddScoped<IPromptResolver, RecordingPromptResolver>();
        DemoRunCalls.UseNamedDataAndUrlPrompt(services, _demoPrompt);
    };

    [OneTimeSetUp]
    public async Task SeedPlanAsync()
    {
        Host.ActAs(SeedIds.Users.Joel);
        _keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, PlanFixture.FeatureEditor);
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO user_preference (user_id, key, json) VALUES (@joel, 'recentProjects', @json::jsonb)",
            new { joel = SeedIds.Users.Joel, json = $"[\"{SeedIds.Projects.Mob}\", \"{SeedIds.Projects.Ship}\"]" });
    }

    [SetUp]
    public void ResetCalls()
    {
        _calls.Clear();
        _agent.Id = PromptAgents.Other;
    }

    private async Task<McpReply> ReadAsync(string tool, string arguments, TestUser? user = null)
        => await CallToolAsync(await TokenAsync(user ?? TestUser.Joel, ReadOnly), tool, arguments);

    [Test]
    public async Task PlanningGuide_WhenProjectGiven_ShouldResolveThePartForThatProject()
    {
        var reply = await ReadAsync("planning_guide", """{"part":"retro","project":"SHIP"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var call = _calls.All.ShouldHaveSingleItem().Request;
        (call.ProjectId, call.Key).ShouldBe((SeedIds.Projects.Ship, PromptKey.Retro));
    }

    [Test]
    public async Task PlanningGuide_WhenNoProject_ShouldResolveTheMostRecentProject()
    {
        var reply = await ReadAsync("planning_guide", """{"part":"fix"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var call = _calls.All.ShouldHaveSingleItem().Request;
        (call.ProjectId, call.Key).ShouldBe((SeedIds.Projects.Mob, PromptKey.Fix));
    }

    [Test]
    public async Task PlanningGuide_WhenCallerInNoProject_ShouldServeTheBuiltInWithoutResolving()
    {
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE project_member SET archived_at = now() WHERE user_id = @kai", new { kai = SeedIds.Users.Kai });
        }

        var reply = await ReadAsync("planning_guide", """{"part":"guide"}""", TestUser.Kai);

        reply.IsToolError().ShouldBeFalse(reply.Body);
        _calls.All.ShouldBeEmpty();
        reply.Structured().GetProperty("markdown").GetString().ShouldBe(Host.Services.GetRequiredService<IBuiltInPrompts>().Text(PromptKey.Guide));
    }

    [Test]
    public async Task GetBrief_WhenTask_ShouldResolveBriefAndReportWithTheTaskKey()
    {
        var task = _keys.Tasks.Values.First();

        var reply = await ReadAsync("get_brief", $$"""{"item":"{{task}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        _calls.All.Select(c => (c.Request.ProjectId, c.Request.Key, c.Request.ItemKey)).ShouldBe(
            [(SeedIds.Projects.Ship, PromptKey.Brief, task), (SeedIds.Projects.Ship, PromptKey.Report, task)], ignoreOrder: true);
    }

    [Test]
    public async Task GetBrief_WhenStory_ShouldResolveLead()
    {
        var story = _keys.Stories.Values.First();

        var reply = await ReadAsync("get_brief", $$"""{"item":"{{story}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var call = _calls.All.ShouldHaveSingleItem().Request;
        (call.ProjectId, call.Key, call.ItemKey).ShouldBe((SeedIds.Projects.Ship, PromptKey.Lead, story));
    }

    [Test]
    public async Task GetBrief_WhenConnectedAgentIsClaudeCode_ShouldPassThatAgent()
    {
        _agent.Id = PromptAgents.ClaudeCode;

        var reply = await ReadAsync("get_brief", $$"""{"item":"{{_keys.Stories.Values.First()}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        _calls.All.ShouldHaveSingleItem().Agent.ShouldBe(PromptAgents.ClaudeCode);
    }

    [Test]
    public async Task CreateSprintDemo_ShouldResolveTheDemoTemplate()
    {
        await using var database = await OpenDatabaseAsync();
        var (project, _, name) = await DemoRunCalls.ArrangeAsync(database, Api);

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadWrite), "create_sprint_demo", DemoRunCalls.ToolArguments(project.Key, name));

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var call = _calls.All.Single(c => c.Request.Key == PromptKey.SprintDemo).Request;
        (call.ProjectId, call.UserId).ShouldBe((project.Id, SeedIds.Users.Joel));
        call.ItemKey.ShouldNotBeNullOrEmpty();
        call.Agent.ShouldBeNull();
    }
}
