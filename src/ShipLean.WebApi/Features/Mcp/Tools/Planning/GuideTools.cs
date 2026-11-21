using ShipLean.WebApi.Infrastructure.PlanningGuide;
using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Mcp.Planning.Lessons;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Prompts;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Planning;
using ShipLean.WebApi.Features.Mcp.Resolve;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

[McpServerToolType]
internal sealed class GuideTools(ProjectResolver projects, IProjectAccess access, IAgentAddresses addresses, ProjectLessons lessons, IPromptResolver prompts, IBuiltInPrompts builtIns)
{
    private const string DefaultPart = "guide";

    [McpServerTool(Name = "planning_guide"), McpScope(McpScopes.Read)]
    [Description("How work moves through ShipLean from idea to done (part work), and how to break a feature or epic down into stories and tasks, from one story to several agents working in parallel (part guide and the templates). Returns one part as Markdown, the names of every part, and what the project's past retros said to change next time. Read work when research might become work; read guide before any breakdown, whatever its size.")]
    public async Task<CallToolResult> PlanningGuide(
        [Description("Which part: work (idea to done), guide (the default for planning), brief, report, plan_reviewer, code_reviewer, fix, lead or retro.")] string? part = null,
        [Description("The project key whose past retros the lessons come from, such as SHIP. The part is served as that project's version of it, and the lessons come from that project's retros. Left out, the part is your most recent project's version, without lessons.")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        // Parse before any project work, so a bad part is a validation error whoever asks.
        var wanted = PlanGuidePart.Parse(string.IsNullOrWhiteSpace(part) ? DefaultPart : part);
        var resolved = string.IsNullOrWhiteSpace(project) ? null : await projects.ResolveAsync(project, forWrite: false, cancellationToken);
        if (resolved is not null)
        {
            await access.RequireAsync(resolved.Id, ProjectRole.Member, cancellationToken);
        }
        // D15: with no project the part is the caller's most recent project's, and only a caller in no project gets the built-in.
        var served = resolved ?? await projects.TryResolveRecentAsync(cancellationToken);
        var text = served is null
            ? builtIns.Text(KeyOf(wanted))
            : (await prompts.ResolveAsync(new PromptRequest(served.Id, KeyOf(wanted)), cancellationToken)).Text;
        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;

        var newest = resolved is null ? [] : await lessons.NewestAsync(resolved.Id, cancellationToken);
        var result = new PlanningGuideResult(
            resolved?.Key, resolved is null ? issuer : McpOpenUrls.Backlog(issuer, resolved.Key, "features"),
            wanted.WireName(), text, Enum.GetValues<PlanGuidePart>().Select(p => p.WireName()).ToList(),
            newest.Select(l => new PlanLesson(l.Feature, l.Retro, l.Text)).ToList());
        return PlanningReply.Of(result, $"The {result.Part} part of the planning guide; the Markdown follows.", result.Markdown);
    }

    private static PromptKey KeyOf(PlanGuidePart part) => part switch
    {
        PlanGuidePart.Work => PromptKey.Work,
        PlanGuidePart.Guide => PromptKey.Guide,
        PlanGuidePart.PlanReviewer => PromptKey.PlanReviewer,
        PlanGuidePart.Lead => PromptKey.Lead,
        PlanGuidePart.Brief => PromptKey.Brief,
        PlanGuidePart.Report => PromptKey.Report,
        PlanGuidePart.CodeReviewer => PromptKey.CodeReviewer,
        PlanGuidePart.Fix => PromptKey.Fix,
        PlanGuidePart.Retro => PromptKey.Retro,
        _ => throw new ArgumentOutOfRangeException(nameof(part), part, "Unknown planning guide part."),
    };
}
