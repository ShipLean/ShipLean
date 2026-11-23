using System.ComponentModel;
using System.Text.Json;
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

// Lists a project's prompts, or reads one: its facts, the text of a version and its versions (Project settings › Prompts). Member.
[McpServerToolType]
internal sealed class PromptsTools(ProjectResolver projects, IProjectAccess access, CaptureSupport support)
{
    [McpServerTool(Name = "prompts"), McpScope(McpScopes.Read)]
    [Description("Read the project's prompts: the instructions ShipLean gives agents at each stage (Project settings › Prompts). Without `prompt` it lists every prompt with its status, the version in use and how many versions it has. With `prompt` it returns that prompt's details, its versions (newest first, with who saved each and why) and the full text of the version in use, or of `version` when given. Read a prompt this way before you change it with save_prompt.")]
    public async Task<CallToolResult> Prompts(
        [Description("The project key, such as SHIP. Needed when the user is in more than one project.")] string? project = null,
        [Description("A prompt's key, such as guide or work. Leave it out to list every prompt.")] string? prompt = null,
        [Description("The version whose text to return. A version number such as 3, or \"built-in\". Defaults to the version in use.")] JsonElement? version = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: false, cancellationToken);
        var role = await access.RequireAsync(resolved.Id, ProjectRole.Member, cancellationToken);
        var baseUrl = await support.BaseUrlAsync(cancellationToken);
        var canEdit = role >= ProjectRole.Admin;
        if (prompt is null)
        {
            // TODO: real prompt list from IPromptReads.PageAsync
            var list = new PromptsListResult(
                resolved.Key, McpOpenUrls.Prompts(baseUrl, resolved.Key),
                [new PromptLine(PromptKeys.ToWire(PromptKey.Guide), "Planning guide", PromptArgs.Wire(PromptStage.Plan), PromptArgs.Wire(PromptStatus.BuiltIn), null, 0, [])],
                canEdit, 0);
            return PlanningReply.Of(list, $"{list.Prompts.Count} prompts, {list.CustomCount} with a custom version.");
        }

        var key = PromptArgs.Key(prompt);
        var textOf = version is null ? null : PromptArgs.Version(version, "version");
        var wire = PromptKeys.ToWire(key);
        // TODO: real prompt details, text and versions from IPromptReads
        var detail = new PromptDetailResult(
            resolved.Key, McpOpenUrls.Prompts(baseUrl, resolved.Key, wire), wire, wire, string.Empty, string.Empty, string.Empty, string.Empty,
            PromptArgs.Wire(PromptStatus.BuiltIn), null, [], [], PromptArgs.Label(textOf), string.Empty, [], canEdit);
        return PlanningReply.Of(detail, $"{wire}: the text of {detail.TextOf} is in the Markdown below.", detail.Text);
    }
}
