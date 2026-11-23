using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Prompts;
using ShipLean.Contracts.Usage;
using ShipLean.WebApi.Infrastructure;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Tools.Prompts;

// Puts a saved version of a prompt back in use, or the built-in: the History tab's Use this version and Use built-in. Admin.
[McpServerToolType]
internal sealed class UsePromptVersionTools(ProjectResolver projects, IProjectAccess access, CaptureSupport support,
    IPromptWrites writes, ICurrentUser currentUser, IUsageEvents events, IUsageScope scope, TimeProvider time)
{
    [McpServerTool(Name = "use_prompt_version"), McpScope(McpScopes.Write)]
    [Description("Choose which version of a project prompt agents get: a saved version number, or built-in to go back to ShipLean's own text. Nothing is deleted; saved versions stay in the prompt's history. Only a project admin can do this, and only when the person you work for asks.")]
    public async Task<CallToolResult> UsePromptVersion(
        [Description("A prompt's key, such as guide or work.")] string prompt,
        [Description("A version number such as 3, or \"built-in\".")] JsonElement version,
        [Description("The project key, such as SHIP. Needed when the user is in more than one project.")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: true, cancellationToken);
        await access.RequireAsync(resolved.Id, ProjectRole.Admin, cancellationToken);
        var key = PromptArgs.Key(prompt);
        var number = PromptArgs.Version(version, "version");
        var wire = PromptKeys.ToWire(key);
        var started = time.GetTimestamp();
        var saved = number is { } target
            ? await writes.UseVersionAsync(resolved.Id, key, target, cancellationToken)
            : await writes.UseBuiltInAsync(resolved.Id, key, cancellationToken);
        SelfEmittedUsage.TrackOk(
            events, scope, time, started, number is null ? UsageEventNames.PromptBuiltInUsed : UsageEventNames.PromptVersionRestored,
            currentUser.Id, "tool:use_prompt_version", resolved.Id,
            new Dictionary<string, object> { [UsageProps.Prompt] = wire }, UsageSurface.Mcp);
        var result = new UsePromptVersionResult(
            resolved.Key, McpOpenUrls.Prompts(await support.BaseUrlAsync(cancellationToken), resolved.Key, wire), wire,
            PromptArgs.Wire(saved.Status), saved.InUseVersion);
        return PlanningReply.Of(result, $"{wire} now uses {PromptArgs.Label(saved.InUseVersion)}.");
    }
}
