using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Refs;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;
using Microsoft.Net.Http.Headers;

namespace ShipLean.WebApi.Features.Mcp.Tools.Attach;

// Files an agent puts on a work item. The store enforces the size limits while it streams the bytes in (nothing is kept over them) and stamps the source (agent and tool); a file of the same name replaces the old one.
[McpServerToolType]
internal sealed class AttachTools(
    ItemResolver itemResolver, IAttachmentStore attachments, ICurrentAgent agent, CaptureSupport support, ITransferTokens transfers, ICurrentUser currentUser)
{
    private const string MarkdownVia = "attach_markdown";
    private const string FileVia = "attach_file";
    private const string MarkdownType = "text/markdown";
    private const int MaxNameLength = 255;

    [McpServerTool(Name = "attach_markdown"), McpScope(McpScopes.Write)]
    [Description("Attach research findings, analyses, comparisons, plans and retros as Markdown on a work item instead of keeping them in local files. The name must end in .md. Attaching a name the item already has replaces that file. It shows on the item as attached by you through this tool, and its text is searchable.")]
    public async Task<CallToolResult> AttachMarkdown(
        [Description("The item's key, such as SHIP-12.")] string feature,
        [Description("The file name, ending in .md, such as design.md.")] string name,
        [Description("The Markdown text.")] string content,
        CancellationToken cancellationToken = default)
    {
        var resolved = await itemResolver.ResolveAsync(feature, ProjectRole.Member, field: "feature", cancellationToken: cancellationToken);
        if (!name.Trim().EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            throw ApiException.Validation("The name of a Markdown file must end in .md.", "name");
        }
        var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
        return await AddAsync(resolved, name, MarkdownType, bytes, MarkdownVia, cancellationToken);
    }

    [McpServerTool(Name = "attach_file"), McpScope(McpScopes.Write)]
    [Description("Attach any file to a work item. For anything but a small file, leave out base64 and use the upload URL: the call returns upload_url and a curl command (curl -T <file> '<url>'); run it in the shell, so the bytes never pass through you. The URL works once, for 10 minutes, up to the attachment size limit (25 MB by default). With base64 the file is sent in the call itself, which is limited to 1 MB (about 700 KB of file): use it only for tiny files. Attaching a name the item already has replaces that file.")]
    public async Task<CallToolResult> AttachFile(
        [Description("The item's key, such as SHIP-12.")] string feature,
        [Description("The file name, such as diagram.png.")] string name,
        [Description("The file's content type, such as image/png.")] string content_type,
        [Description("The file's bytes, base64 encoded. Leave out to get an upload URL instead.")] string? base64 = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await itemResolver.ResolveAsync(feature, ProjectRole.Member, field: "feature", cancellationToken: cancellationToken);
        if (base64 is null)
        {
            return await UploadUrlAsync(resolved, name, content_type, cancellationToken);
        }

        var text = base64.Trim();
        var bytes = new byte[text.Length];
        if (!Convert.TryFromBase64String(text, bytes, out var written))
        {
            throw ApiException.Validation("base64 is not valid base64 text.", "base64");
        }
        return await AddAsync(resolved, name, content_type, bytes.AsMemory(0, written).ToArray(), FileVia, cancellationToken);
    }

    private async Task<CallToolResult> UploadUrlAsync(ResolvedItem resolved, string name, string contentType, CancellationToken cancellationToken)
    {
        var cleanName = (name ?? string.Empty).Trim();
        if (cleanName.Length == 0 || cleanName.Length > MaxNameLength || cleanName.Any(char.IsControl))
        {
            throw ApiException.Validation($"name must be a file name of 1 to {MaxNameLength} characters with no control characters.", "name");
        }

        if (!MediaTypeHeaderValue.TryParse(contentType, out var parsed) || !parsed.MediaType.HasValue)
        {
            throw ApiException.Validation("content_type must be a content type such as image/png.", "content_type");
        }

        var issued = await transfers.IssueAsync(
            new NewTransfer(TransferKind.Upload, resolved.Id, null, cleanName, contentType.Trim(), currentUser.Id,
                support.AgentAuthorizationId(), AgentStamp.Of(agent) ?? "Agent"),
            cancellationToken);
        var project = await support.ProjectOfAsync(resolved, cancellationToken);
        var baseUrl = await support.BaseUrlAsync(cancellationToken);
        var url = $"{baseUrl.TrimEnd('/')}/api/uploads/{issued.Token}";
        var command = $"curl -T <file> '{url}'";
        var result = new UploadUrlResult(project.Key, McpOpenUrls.WorkItem(baseUrl, project.Key, resolved.Key), resolved.Key, cleanName, url, issued.ExpiresAt, command);
        return CaptureSupport.Reply(result, $"Nothing is attached yet. Run this in the shell, with the file's path in place of <file>, within 10 minutes: {command}");
    }

    private async Task<CallToolResult> AddAsync(ResolvedItem resolved, string name, string contentType, byte[] bytes, string via, CancellationToken cancellationToken)
    {
        var before = await attachments.ListAsync(resolved.Id, cancellationToken);
        await using var stream = new MemoryStream(bytes);
        var added = await attachments.AddAsync(resolved.Id, name, contentType, stream, new AttachmentSource(AgentStamp.Of(agent) ?? "Agent", via), cancellationToken);
        var project = await support.ProjectOfAsync(resolved, cancellationToken);
        var result = new AttachResult(
            project.Key, McpOpenUrls.WorkItem(await support.BaseUrlAsync(cancellationToken), project.Key, resolved.Key), resolved.Key,
            added.Id, added.Name, added.Kind.ToString().ToLowerInvariant(), added.SizeBytes, before.Any(a => a.Name == added.Name && a.Id != added.Id), added.Via);
        return CaptureSupport.Reply(result, $"Attached {added.Name} to {resolved.Key}{(result.Replaced ? ", replacing the earlier file of that name" : string.Empty)}.");
    }
}
