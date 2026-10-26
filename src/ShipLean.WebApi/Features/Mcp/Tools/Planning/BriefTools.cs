using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Mcp.Planning.Markers;
using ShipLean.Application.Features.Mcp.Planning.Reads;
using ShipLean.Application.Features.Mcp.Planning.Tree;
using ShipLean.Application.Features.Mcp.Planning.Waves;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Planning;
using ShipLean.WebApi.Features.Mcp.Planning.Briefs;
using ShipLean.WebApi.Features.Mcp.Resolve;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

[McpServerToolType]
internal sealed class BriefTools(ItemResolver items, IPlanReads plans, IAttachmentStore attachments, ICommentStore comments, IAgentAddresses addresses)
{
    [McpServerTool(Name = "get_brief"), McpScope(McpScopes.Read)]
    [Description("The brief to work from for an item of a plan capture_plan created. For a task, the whole brief its agent follows: the common brief, the task's own brief, the shared branch and the report to send back with report_task. For a story, the lead's brief for that lane. For a feature, the conductor's view: its waves in order, each with its stories and whether they are done, and the first wave that can start.")]
    public async Task<CallToolResult> GetBrief(
        [Description("The key of a task, story or feature in a plan, such as SHIP-12.")] string item,
        CancellationToken cancellationToken = default)
    {
        var resolved = await items.ResolveAsync(item, ProjectRole.Member, cancellationToken: cancellationToken);
        var (tree, itemId) = await plans.TreeOfAsync(resolved.Id, cancellationToken);
        var projectKey = PlanningReply.ProjectKeyOf(resolved.Key);
        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;
        var openUrl = McpOpenUrls.WorkItem(issuer, projectKey, resolved.Key);

        var prefix = await BriefPlanContext.PrefixAsync(attachments, comments, tree, StoryId(tree, itemId), cancellationToken);
        BriefResult result;
        if (itemId == tree.Feature.Id)
        {
            result = FeatureBrief(tree, projectKey, openUrl, prefix);
        }
        else if (tree.Stories.FirstOrDefault(story => story.Id == itemId) is { } story)
        {
            result = new BriefResult(projectKey, openUrl, resolved.Key, "story", Prefix(prefix, BriefMarkdown.ForStory(tree, story)), null, null);
        }
        else
        {
            result = await TaskBriefAsync(tree, itemId, projectKey, openUrl, resolved.Key, prefix, cancellationToken);
        }
        return PlanningReply.Of(result, $"The brief for {resolved.Key}.");
    }

    private static Guid? StoryId(PlanTree tree, Guid itemId)
        => tree.Stories.FirstOrDefault(story => story.Id == itemId)?.Id
            ?? tree.Stories.FirstOrDefault(story => story.Tasks.Any(task => task.Id == itemId))?.Id;

    private static string Prefix(string rule, string markdown) => rule + "\n\n" + markdown;

    private static BriefResult FeatureBrief(PlanTree tree, string projectKey, string openUrl, string prefix)
    {
        var waves = PlanWaves.Group(tree);
        var startable = PlanWaves.Startable(tree);
        var view = waves.Select(wave => new BriefWave(
            wave.Wave.Text,
            wave.Done,
            [.. wave.Stories.Select(story => new BriefStory(story.Key, story.Title, story.Lane, story.StateName, PlanWaves.IsDone(story)))])).ToList();
        return new BriefResult(projectKey, openUrl, tree.Feature.Key, "feature", Prefix(prefix, BriefMarkdown.ForFeature(tree, waves, startable)), view, startable?.Text);
    }

    private async Task<BriefResult> TaskBriefAsync(PlanTree tree, Guid taskId, string projectKey, string openUrl, string key, string prefix, CancellationToken cancellationToken)
    {
        var task = tree.Stories.SelectMany(story => story.Tasks).First(candidate => candidate.Id == taskId);
        var markdown = BriefMarkdown.ForTask(tree, task, await CommonBriefAsync(tree.Feature.Id, cancellationToken), PlanGuide.Read(PlanGuidePart.Report));
        return new BriefResult(projectKey, openUrl, key, "task", Prefix(prefix, markdown), null, null);
    }

    // The Feature's live common-brief.md, null when it has none.
    private async Task<string?> CommonBriefAsync(Guid featureId, CancellationToken cancellationToken)
    {
        var files = await attachments.ListAsync(featureId, cancellationToken);
        var file = files.FirstOrDefault(candidate => string.Equals(candidate.Name, PlanFiles.CommonBrief, StringComparison.Ordinal));
        if (file is null)
        {
            return null;
        }

        var (_, blob) = await attachments.OpenAsync(file.Id, cancellationToken);
        using var reader = new StreamReader(blob.Content);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
