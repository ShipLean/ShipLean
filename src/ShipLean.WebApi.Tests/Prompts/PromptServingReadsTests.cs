using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Prompts;

namespace ShipLean.WebApi.Tests.Prompts;

// C8 (AC11): the servings an Inspection run shows, matched by item and time (D19).
[TestFixture]
public class PromptServingReadsTests : ApiFixture
{
    private static readonly DateTimeOffset Start = TimeProvider.System.GetUtcNow().AddHours(-2);

    private Task<Guid> ServeAsync(PromptKey key, string? item, DateTimeOffset at, string agent = PromptAgents.ClaudeCode)
        => new PromptServingBuilder(SeedIds.Projects.Ship, key).WithItemKey(item).WithServedAt(at).WithAgent(agent).InsertAsync(ConnectionString);

    private async Task<IReadOnlyList<PromptServingDto>> ForRunAsync(string[] items, string? agent = null)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPromptServingReads>().ForRunAsync("SHIP", items, agent, Start, Start.AddMinutes(30));
    }

    [Test]
    public async Task ForRun_WhenServedInTheWindow_ShouldListThem()
    {
        await ServeAsync(PromptKey.Work, "SHIP-30", Start.AddMinutes(1));
        await ServeAsync(PromptKey.Brief, "SHIP-30", Start.AddMinutes(10));

        var servings = await ForRunAsync(["SHIP-30"]);

        servings.Select(s => s.Key).ShouldBe([PromptKey.Brief, PromptKey.Work]);
    }

    [Test]
    public async Task ForRun_WhenOutsideTheWindow_ShouldLeaveThemOut()
    {
        await ServeAsync(PromptKey.Work, "SHIP-31", Start.AddMinutes(-1));
        await ServeAsync(PromptKey.Brief, "SHIP-31", Start.AddMinutes(31));
        await ServeAsync(PromptKey.Report, "SHIP-31", Start.AddMinutes(5));

        var servings = await ForRunAsync(["SHIP-31"]);

        servings.Select(s => s.Key).ShouldBe([PromptKey.Report]);
    }

    [Test]
    public async Task ForRun_WhenAnotherItem_ShouldLeaveItOut()
    {
        await ServeAsync(PromptKey.Work, "SHIP-32", Start.AddMinutes(1));
        await ServeAsync(PromptKey.Brief, "SHIP-33", Start.AddMinutes(1));
        await ServeAsync(PromptKey.Guide, null, Start.AddMinutes(1));

        var servings = await ForRunAsync(["SHIP-32"]);

        servings.Select(s => s.Key).ShouldBe([PromptKey.Work]);
    }

    [Test]
    public async Task ForRun_WhenAgentGiven_ShouldKeepOnlyThatAgent()
    {
        await ServeAsync(PromptKey.Work, "SHIP-34", Start.AddMinutes(1), PromptAgents.ClaudeCode);
        await ServeAsync(PromptKey.Brief, "SHIP-34", Start.AddMinutes(2), PromptAgents.Cursor);

        var cursor = await ForRunAsync(["SHIP-34"], PromptAgents.Cursor);
        var other = await ForRunAsync(["SHIP-34"], PromptAgents.Other);

        cursor.Select(s => s.Key).ShouldBe([PromptKey.Brief]);
        other.Count.ShouldBe(2);
    }

    [Test]
    public async Task ForRun_WhenCustom_ShouldLabelTheVersionAndAgent()
    {
        var versionId = await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Brief).WithText("Custom brief.").InUse().InsertAsync(ConnectionString);
        await new PromptServingBuilder(SeedIds.Projects.Ship, PromptKey.Brief).WithVersion(versionId, 2).WithItemKey("SHIP-35").WithServedAt(Start.AddMinutes(1))
            .WithAgent(PromptAgents.ClaudeCode).InsertAsync(ConnectionString);
        await ServeAsync(PromptKey.Work, "SHIP-35", Start.AddMinutes(2), PromptAgents.Other);

        var servings = await ForRunAsync(["SHIP-35"]);

        servings.Select(s => s.Label).ShouldBe(["work.md built-in · Another agent", "brief.md custom v2 · Claude Code"]);
    }
}
