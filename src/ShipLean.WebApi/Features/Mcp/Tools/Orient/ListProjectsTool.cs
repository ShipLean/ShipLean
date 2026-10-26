using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Mcp.Orient;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;

namespace ShipLean.WebApi.Features.Mcp.Tools.Orient;

[McpServerToolType]
internal sealed class ListProjectsTool(OrientReads reads, ProjectResolver projects, IProjectAccess access, IAgentAddresses addresses)
{
    [McpServerTool(Name = "list_projects"), McpScope(McpScopes.Read)]
    [Description("List the projects I am in: each one's key, name, my role and the sprint running now. The first call to make: it shows the project keys the other tools take. Give a project key to see only that one.")]
    public async Task<CallToolResult> ListProjectsAsync(
        [Description("A project key to look at just that project (optional).")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        var only = string.IsNullOrWhiteSpace(project) ? null : await projects.ResolveAsync(project, forWrite: false, cancellationToken);
        if (only is not null)
        {
            await access.RequireAsync(only.Id, ProjectRole.Member, cancellationToken);
        }
        var found = await reads.ProjectsAsync(only?.Id, cancellationToken);
        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;

        var result = new ListProjectsResult(
            only?.Key,
            only is null ? issuer : McpOpenUrls.Sprints(issuer, only.Key),
            [.. found.Select(p => new ListProjectsResult.ProjectLine(
                p.Key, p.Name, p.Role.ToString().ToLowerInvariant(),
                p.CurrentSprint is { } sprint ? new ListProjectsResult.SprintLine(sprint.Name, sprint.Start, sprint.End) : null))]);
        return OrientReply.Of(result, $"{result.Projects.Count} project{(result.Projects.Count == 1 ? string.Empty : "s")}: {string.Join(", ", result.Projects.Select(p => p.Key))}.");
    }
}
