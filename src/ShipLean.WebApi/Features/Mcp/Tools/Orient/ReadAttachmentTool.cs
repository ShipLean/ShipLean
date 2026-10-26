using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Planning;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;
using ShipLean.WebApi.Features.Mcp.Resolve;

namespace ShipLean.WebApi.Features.Mcp.Tools.Orient;

[McpServerToolType]
internal sealed class ReadAttachmentTool(ItemResolver items, IAttachmentStore attachments, IAgentAddresses addresses)
{
    [McpServerTool(Name = "read_attachment"), McpScope(McpScopes.Read)]
    [Description("Read a Markdown attachment on a work item by name. Returns the text when it is at most 200 KB; larger Markdown files say so and give the UI link. Other files return their metadata and the UI link.")]
    public async Task<CallToolResult> ReadAttachmentAsync(
        [Description("The work item's key, such as SHIP-12.")] string item,
        [Description("The attachment file name, such as research-2026-10-26.md.")] string name,
        CancellationToken cancellationToken = default)
    {
        var resolved = await items.ResolveAsync(item, ProjectRole.Member, field: "item", cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw ApiException.Validation("Pass the attachment name.", "name");
        }

        var file = (await attachments.ListAsync(resolved.Id, cancellationToken))
            .LastOrDefault(candidate => string.Equals(candidate.Name, name.Trim(), StringComparison.Ordinal));
        if (file is null)
        {
            throw ApiException.NotFound($"No attachment named \"{name.Trim()}\" on {resolved.Key}.");
        }

        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;
        var projectKey = PlanningReply.ProjectKeyOf(resolved.Key);
        var openUrl = McpOpenUrls.WorkItemAttachment(issuer, projectKey, resolved.Key, file.Id);
        var isMarkdown = file.Kind == AttachmentKind.Markdown;

        string? markdown = null;
        var tooLarge = false;
        if (isMarkdown && file.SizeBytes <= PlanShapeRules.MaxPlanMarkdownBytes)
        {
            var (_, blob) = await attachments.OpenAsync(file.Id, cancellationToken);
            await using (blob.Content)
            {
                using var reader = new StreamReader(blob.Content, Encoding.UTF8);
                markdown = await reader.ReadToEndAsync(cancellationToken);
            }
        }
        else if (isMarkdown)
        {
            tooLarge = true;
        }

        var result = new ReadAttachmentResult(
            projectKey, openUrl, resolved.Key, file.Name, file.ContentType, file.SizeBytes, isMarkdown, markdown, tooLarge);
        string summary;
        if (markdown is not null)
        {
            summary = $"{file.Name} on {resolved.Key}: {file.SizeBytes:N0} bytes of Markdown.";
        }
        else if (tooLarge)
        {
            summary = $"{file.Name} on {resolved.Key} is over 200 KB; open it in the UI.";
        }
        else
        {
            summary = $"{file.Name} on {resolved.Key} is not Markdown; open it in the UI.";
        }
        return OrientReply.Of(result, summary);
    }
}
