using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Prompts;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Plan;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Tools.Prompts;

// Saves a new custom version of a project prompt, as the editor's Save does. Without `plan` it previews (the checks, the diff, the warnings)
// and returns a plan; with the plan and the same arguments it saves. Admin.
[McpServerToolType]
internal sealed class SavePromptTools(ProjectResolver projects, IProjectAccess access, CaptureSupport support, PlanEnvelope envelope)
{
    private const string PlanKind = "save_prompt";

    [McpServerTool(Name = "save_prompt"), McpScope(McpScopes.Plan, PreviewScope = McpScopes.Read)]
    [Description("Save a new version of a project prompt with the full new text and a note saying what changed; agents get it from then on. Read the prompt with prompts first and pass the version you read as `based_on`. Without `plan` it only previews: the checks, a diff against the version in use, and warnings such as a missing placeholder. Call again with the same arguments and the `plan` it returned to save. If someone saved in between, the save is refused: read the prompt again. Only a project admin can save, and only when the person you work for asks.")]
    public async Task<CallToolResult> SavePrompt(
        [Description("A prompt's key, such as guide or work.")] string prompt,
        [Description("The full new text of the prompt, not a diff.")] string text,
        [Description("What you changed and why, at most 200 characters. It shows in the prompt's history.")] string note,
        [Description("The version you read as in use with prompts. A version number such as 3, or \"built-in\".")] JsonElement based_on,
        [Description("The project key, such as SHIP. Needed when the user is in more than one project.")] string? project = null,
        [Description("The plan a preview returned. Pass it with the same arguments to save.")] string? plan = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: true, cancellationToken);
        await access.RequireAsync(resolved.Id, ProjectRole.Admin, cancellationToken);
        var key = PromptArgs.Key(prompt);
        var basedOn = PromptArgs.Version(based_on, "based_on");
        var wire = PromptKeys.ToWire(key);
        var openUrl = McpOpenUrls.Prompts(await support.BaseUrlAsync(cancellationToken), resolved.Key, wire);
        var sealedArgs = new SavePromptPlan(wire, PromptHash.Of(text), PromptHash.Of(note), basedOn);

        if (plan is not null)
        {
            if (envelope.Open<SavePromptPlan>(PlanKind, plan, resolved.Id) != sealedArgs)
            {
                throw ApiException.Validation("The arguments changed since the preview. Preview again.", "plan");
            }

            // TODO: real save with IPromptWrites.SaveAsync (source mcp, expected based_on) and IPromptSavePreview.Warnings
            var saved = new SavePromptResult(resolved.Key, openUrl, true, wire, null, null, null, null, 1, PromptArgs.Wire(PromptStatus.Custom), [], null, null);
            return PlanningReply.Of(saved, $"Saved {wire} v{saved.Version}.");
        }

        // TODO: real preview with IPromptSavePreview.PreviewAsync
        var preview = new SavePromptResult(
            resolved.Key, openUrl, false, wire, PromptArgs.Label(basedOn), 1, string.Empty, 0, null, null, [],
            envelope.Seal(PlanKind, resolved.Id, sealedArgs), (int)PlanEnvelope.Lifetime.TotalMinutes);
        return PlanningReply.Of(preview, $"Preview: {wire} would be saved as v{preview.NextVersion}. Nothing was written; call save_prompt again with the same arguments and the `plan` value from the JSON below to save it.");
    }
}
