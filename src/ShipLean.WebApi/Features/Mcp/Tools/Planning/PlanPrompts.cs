using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Mcp.Planning.Lessons;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Mcp.Resolve;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// Prompts are not tool calls, so they need no scope: the tools the message asks for check their own. A failure here is thrown as
// an ApiException and PromptErrorFilter turns it into a JSON-RPC error.
[McpServerPromptType]
internal sealed class PlanPrompts(ItemResolver items, FeatureKind features)
{
    [McpServerPrompt(Name = "plan_feature")]
    [Description("Plan a feature for several agents working in parallel, following ShipLean's planning guide, and put the plan in the backlog.")]
    public async Task<PromptMessage> PlanFeature(
        [Description("The key of the feature to plan, such as SHIP-12.")] string? feature = null,
        [Description("What to build, when there is no feature yet.")] string? description = null,
        CancellationToken cancellationToken = default)
    {
        string what;
        if (!string.IsNullOrWhiteSpace(feature))
        {
            var resolved = await items.ResolveAsync(feature, ProjectRole.Member, field: "feature", cancellationToken: cancellationToken);
            if (!await features.IsFeatureAsync(resolved.Id, cancellationToken))
            {
                throw ApiException.Validation($"{resolved.Key} is not a feature. Give the key of the feature to plan.", "feature");
            }
            what = $"the feature {resolved.Key} (read its attachments with read_attachment, its log with get_work_item, then plan under it with capture_plan by key)";
        }
        else if (!string.IsNullOrWhiteSpace(description))
        {
            what = $"this new feature: {description.Trim()}";
        }
        else
        {
            throw ApiException.Validation("Give the key of the feature to plan, or a description of what to build.", "feature");
        }

        return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"""
                    Call planning_guide first and follow it. Then plan {what} for several agents working in parallel: waves, lanes, a brief per task, and the contracts between them. Preview the plan with capture_plan, fix what its warnings name, and apply it only when the preview is right.
                    """,
            },
        };
    }

    [McpServerPrompt(Name = "research_idea")]
    [Description("Research something that might become work: find or create a Feature, read what is already on it, research, attach findings, update the body, then comment and set_status.")]
    public async Task<PromptMessage> ResearchIdea(
        [Description("The key of an existing Feature, such as SHIP-12.")] string? feature = null,
        [Description("What to look into, when there is no Feature yet.")] string? description = null,
        CancellationToken cancellationToken = default)
    {
        string what;
        if (!string.IsNullOrWhiteSpace(feature))
        {
            var resolved = await items.ResolveAsync(feature, ProjectRole.Member, field: "feature", cancellationToken: cancellationToken);
            what = $"the existing Feature {resolved.Key}";
        }
        else if (!string.IsNullOrWhiteSpace(description))
        {
            what = $"this idea: {description.Trim()}";
        }
        else
        {
            throw ApiException.Validation("Give the key of the Feature to research, or a description of the idea.", "feature");
        }

        return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"""
                    Call planning_guide with part work and follow it for {what}. Search first; reuse an existing Feature or create one in a Proposed state with create_feature. Read what is already there with get_work_item and read_attachment. Do the research, attach findings with attach_markdown, update the Feature body's key facts and decisions, link sources with link when helpful, then end with comment and set_status. Do not keep research in local files.
                    """,
            },
        };
    }
}
