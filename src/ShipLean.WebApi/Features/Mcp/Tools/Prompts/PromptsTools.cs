using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Prompts;
using System.Text;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Tools.Prompts;

// Lists a project's prompts, or reads one: its facts, the text of a version and its versions (Ways of working). Member.
[McpServerToolType]
internal sealed class PromptsTools(ProjectResolver projects, IProjectAccess access, CaptureSupport support, IPromptReads reads)
{
    [McpServerTool(Name = "prompts"), McpScope(McpScopes.Read)]
    [Description("Read the project's prompts: the instructions ShipLean gives agents at each stage (Ways of working). Without `prompt` it lists every prompt with its status, the version in use and how many versions it has. With `prompt` it returns that prompt's details, its versions (newest first, with who saved each and why) and the full text of the version in use, or of `version` when given. Read a prompt this way before you change it with save_prompt.")]
    public async Task<CallToolResult> Prompts(
        [Description("The project key, such as SHIP. Needed when the user is in more than one project.")] string? project = null,
        [Description("A prompt's key, such as guide or work. Leave it out to list every prompt.")] string? prompt = null,
        [Description("The version whose text to return. A version number such as 3, or \"built-in\". Defaults to the version in use.")] JsonElement? version = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: false, cancellationToken);
        var role = await access.RequireAsync(resolved.Id, ProjectRole.Member, cancellationToken);
        var baseUrl = await support.BaseUrlAsync(cancellationToken);
        var page = await reads.PageAsync(resolved.Id, role, cancellationToken);
        if (prompt is null)
        {
            return await ListAsync(resolved, baseUrl, page, cancellationToken);
        }

        var key = PromptArgs.Key(prompt);
        var wanted = version is null ? (int?)null : PromptArgs.Version(version, "version");
        var asked = version is not null;
        var dto = page.Stages.SelectMany(stage => stage.Prompts).Single(p => p.Key == key);
        var history = await reads.HistoryAsync(resolved.Id, key, cancellationToken);
        var inUse = history.Versions.FirstOrDefault(v => v.InUse);
        var (textOf, text) = TextFor(dto, history, inUse, asked, wanted);
        var wire = PromptKeys.ToWire(key);
        var detail = new PromptDetailResult(
            resolved.Key, McpOpenUrls.Prompts(baseUrl, resolved.Key, wire), wire, dto.Name, dto.File, dto.WhenRuns, dto.ServedBy, dto.ReadBy,
            PromptArgs.Wire(dto.Status), inUse?.Number, dto.Placeholders, dto.InsertedParts, textOf, text,
            history.Versions.Select(v => new PromptVersionLine(v.Number, v.Note, v.By, v.Agent, v.AtLabel, PromptKeys.ToWire(v.Source), v.InUse)).ToList(),
            role >= ProjectRole.Admin);
        return PlanningReply.Of(detail, $"{wire}: the text of {textOf} is in the Markdown below.", text);
    }

    private async Task<CallToolResult> ListAsync(ResolvedProject resolved, string baseUrl, PromptsPageDto page, CancellationToken cancellationToken)
    {
        var lines = new List<PromptLine>();
        foreach (var stage in page.Stages)
        {
            foreach (var dto in stage.Prompts)
            {
                var count = dto.Custom is null ? 0 : (await reads.HistoryAsync(resolved.Id, dto.Key, cancellationToken)).Versions.Count;
                var inUse = dto.Custom is { InUse: true } custom ? custom.Number : (int?)null;
                lines.Add(new PromptLine(PromptKeys.ToWire(dto.Key), dto.Name, PromptArgs.Wire(stage.Id), PromptArgs.Wire(dto.Status), inUse, count, dto.Placeholders));
            }
        }

        var list = new PromptsListResult(resolved.Key, McpOpenUrls.Prompts(baseUrl, resolved.Key), lines, page.CanEdit, page.CustomCount);
        var table = new StringBuilder("| Prompt | Stage | Status | In use | Versions |\n|---|---|---|---|---|\n");
        foreach (var line in lines)
        {
            table.Append($"| {line.Key} | {line.Stage} | {line.Status} | {(line.InUseVersion is { } n ? $"v{n}" : "built-in")} | {line.Versions} |\n");
        }

        return PlanningReply.Of(list, $"{lines.Count} prompts in {resolved.Key}: {page.CustomCount} custom.", table.ToString());
    }

    private static (string TextOf, string Text) TextFor(PromptDto dto, PromptHistoryDto history, PromptVersionDto? inUse, bool asked, int? wanted)
    {
        if (!asked)
        {
            return inUse is null ? (PromptArgs.BuiltIn, dto.BuiltInText) : (PromptArgs.Label(inUse.Number), inUse.Text);
        }

        if (wanted is not { } number)
        {
            return (PromptArgs.BuiltIn, dto.BuiltInText);
        }

        var found = history.Versions.FirstOrDefault(v => v.Number == number)
            ?? throw ApiException.NotFound($"There is no version {number} of {PromptKeys.ToWire(dto.Key)}.", "version");
        return (PromptArgs.Label(number), found.Text);
    }
}
