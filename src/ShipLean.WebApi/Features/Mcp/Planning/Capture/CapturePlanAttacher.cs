using System.Text;
using Agoda.IoC.Core;
using ShipLean.Application.Features.Mcp.Planning.Markers;
using ShipLean.Contracts;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Agents;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Planning.Capture;

// Attaches the common brief, and the plan document when one came, to the Feature. The attachment store replaces a file of the
// same name itself, in one transaction, so a failed add leaves the earlier file in place.
[RegisterPerRequest]
public sealed class CapturePlanAttacher(IAttachmentStore attachments, ICurrentAgent agent)
{
    private const string Via = "capture_plan";
    private const string MarkdownType = "text/markdown";

    public async Task<IReadOnlyList<string>> AttachAsync(Guid featureId, CapturePlanInput input, CancellationToken cancellationToken)
    {
        var names = new List<string> { PlanFiles.CommonBrief };
        await AddAsync(featureId, PlanFiles.CommonBrief, input.CommonBrief!, cancellationToken);
        if (input.PlanMarkdown is { } plan)
        {
            names.Add(PlanFiles.Plan);
            await AddAsync(featureId, PlanFiles.Plan, plan, cancellationToken);
        }
        return names;
    }

    // For a retry of an applied plan: adds only the files the Feature lacks, so a file a person replaced since is kept.
    public async Task<IReadOnlyList<string>> AttachMissingAsync(Guid featureId, CapturePlanInput input, CancellationToken cancellationToken)
    {
        var present = (await attachments.ListAsync(featureId, cancellationToken)).Select(file => file.Name).ToHashSet(StringComparer.Ordinal);
        var names = new List<string> { PlanFiles.CommonBrief };
        if (!present.Contains(PlanFiles.CommonBrief))
        {
            await AddAsync(featureId, PlanFiles.CommonBrief, input.CommonBrief!, cancellationToken);
        }
        if (input.PlanMarkdown is { } plan)
        {
            names.Add(PlanFiles.Plan);
            if (!present.Contains(PlanFiles.Plan))
            {
                await AddAsync(featureId, PlanFiles.Plan, plan, cancellationToken);
            }
        }
        return names;
    }

    private async Task AddAsync(Guid featureId, string name, string markdown, CancellationToken cancellationToken)
    {
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
        await attachments.AddAsync(featureId, name, MarkdownType, content, new AttachmentSource(AgentStamp.Of(agent) ?? "Agent", Via), cancellationToken);
    }
}
