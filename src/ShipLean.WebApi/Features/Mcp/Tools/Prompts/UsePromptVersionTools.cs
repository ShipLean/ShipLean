using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Prompts;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Tools.Prompts;

// Puts a saved version of a prompt back in use, or the built-in: the History tab's Use this version and Use built-in. Admin.
[McpServerToolType]
internal sealed class UsePromptVersionTools(ProjectResolver projects, IProjectAccess access, CaptureSupport support)
{
    [McpServerTool(Name = "use_prompt_version"), McpScope(McpScopes.Write)]
    [Description("Choose which version of a project prompt agents get: a saved version number, or built-in to go back to ShipLean's own text. Nothing is deleted; saved versions stay in the prompt's history. Only a project admin can do this, and only when the person you work for asks.")]
    public async Task<CallToolResult> UsePromptVersion(
        [Description("A prompt's key, such as guide or work.")] string prompt,
        [Description("A version number such as 3, or built-in.")] string version,
        [Description("The project key, such as SHIP. Needed when the user is in more than one project.")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: true, cancellationToken);
        await access.RequireAsync(resolved.Id, ProjectRole.Admin, cancellationToken);
        var key = PromptArgs.Key(prompt);
        var number = PromptArgs.Version(version, "version");
        var wire = PromptKeys.ToWire(key);
        // TODO: real move with IPromptWrites.UseVersionAsync / UseBuiltInAsync
        var result = new UsePromptVersionResult(
            resolved.Key, McpOpenUrls.Prompts(await support.BaseUrlAsync(cancellationToken), resolved.Key, wire), wire,
            PromptArgs.Wire(number is null ? PromptStatus.UsingBuiltIn : PromptStatus.Custom), number);
        return PlanningReply.Of(result, $"{wire} now uses {PromptArgs.Label(number)}.");
    }
}
