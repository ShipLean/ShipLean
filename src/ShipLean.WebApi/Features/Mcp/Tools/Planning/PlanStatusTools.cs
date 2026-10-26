using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Mcp.Planning.Reads;
using ShipLean.Application.Features.Mcp.Planning.Status;
using ShipLean.Application.Features.Mcp.Planning.Tree;
using ShipLean.Application.Features.Mcp.Planning.Waves;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Refs;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

[McpServerToolType]
internal sealed class PlanStatusTools(ItemResolver items, IPlanReads plans, IPlanStatusReads status, IAgentAddresses addresses)
{
    [McpServerTool(Name = "plan_status"), McpScope(McpScopes.Read)]
    [Description("How a plan capture_plan created is going: its waves in order, each story with its state and who has it, and each task with its state, model role, context size, when it started and finished, its commits, how many review rounds it took and whether it was left orphaned. Also the first wave that can start. Use it to run the plan wave by wave and to write its retro.")]
    public async Task<CallToolResult> PlanStatus(
        [Description("The plan's feature key, such as SHIP-12.")] string item,
        CancellationToken cancellationToken = default)
    {
        var resolved = await items.ResolveAsync(item, ProjectRole.Member, cancellationToken: cancellationToken);
        var tree = await plans.TreeAsync(resolved.Id, cancellationToken);
        var projectKey = PlanningReply.ProjectKeyOf(resolved.Key);
        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;
        var activity = await status.ActivityAsync([.. tree.Stories.SelectMany(story => story.Tasks).Select(task => task.Id)], cancellationToken);

        var waves = PlanWaves.Group(tree).Select(wave => new PlanStatusWave(
            wave.Wave.Text,
            wave.Done,
            [.. wave.Stories.Select(story => new PlanStatusStory(
                story.Key, story.Title, story.Lane, story.StateName, story.Assignee,
                [.. story.Tasks.Select(task => TaskStatus(task, activity[task.Id]))]))])).ToList();
        var result = new PlanStatusResult(projectKey, McpOpenUrls.WorkItem(issuer, projectKey, resolved.Key), tree.Feature.Key, waves, PlanWaves.Startable(tree)?.Text);
        return PlanningReply.Of(result, $"The plan under {resolved.Key}.");
    }

    private static PlanStatusTask TaskStatus(PlanTask task, PlanTaskActivity activity) => new(
        task.Key, task.Title, task.StateName, CategoryWord(task.Category), task.Assignee, task.Tags.Model, task.Tags.ContextK,
        activity.StartedAt, task.Category == StateCategory.Completed ? activity.FinishedAt : null,
        [.. activity.Commits.Select(commit => new PlanCommit(commit.Sha, commit.Subject, commit.Url))],
        activity.ReviewRounds, task.Orphan);

    // The stored camelCase word, as sprint_status writes it.
    private static string CategoryWord(StateCategory category) => JsonNamingPolicy.CamelCase.ConvertName(category.ToString());
}
