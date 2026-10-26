using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Mcp.Planning.Lessons;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Planning;
using ShipLean.WebApi.Features.Mcp.Resolve;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

[McpServerToolType]
internal sealed class GuideTools(ProjectResolver projects, IProjectAccess access, IAgentAddresses addresses, ProjectLessons lessons)
{
    private const string DefaultPart = "guide";

    [McpServerTool(Name = "planning_guide"), McpScope(McpScopes.Read)]
    [Description("How work moves through ShipLean from idea to done (part work), and how to break a feature or epic down for several agents working in parallel (part guide and the templates). Returns one part as Markdown, the names of every part, and what the project's past retros said to change next time. Read work when research might become work; read guide before you plan.")]
    public async Task<CallToolResult> PlanningGuide(
        [Description("Which part: work (idea to done), guide (the default for planning), brief, report, plan_reviewer, code_reviewer, fix, lead or retro.")] string? part = null,
        [Description("The project key whose past retros the lessons come from, such as SHIP. Leave out for the guide alone; with it, the lessons come from that project's retros.")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        // Parse before any project work, so a bad part is a validation error whoever asks.
        var wanted = PlanGuidePart.Parse(string.IsNullOrWhiteSpace(part) ? DefaultPart : part);
        var resolved = string.IsNullOrWhiteSpace(project) ? null : await projects.ResolveAsync(project, forWrite: false, cancellationToken);
        if (resolved is not null)
        {
            await access.RequireAsync(resolved.Id, ProjectRole.Member, cancellationToken);
        }
        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;

        var newest = resolved is null ? [] : await lessons.NewestAsync(resolved.Id, cancellationToken);
        var result = new PlanningGuideResult(
            resolved?.Key, resolved is null ? issuer : McpOpenUrls.Backlog(issuer, resolved.Key, "features"),
            wanted.WireName(), PlanGuide.Read(wanted), Enum.GetValues<PlanGuidePart>().Select(p => p.WireName()).ToList(),
            newest.Select(l => new PlanLesson(l.Feature, l.Retro, l.Text)).ToList());
        return PlanningReply.Of(result, $"The {result.Part} part of the planning guide.");
    }
}
