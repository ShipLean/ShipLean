using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Writes;

namespace ShipLean.WebApi.Features.Mcp.Tools.Capture;

[McpServerToolType]
internal sealed class DiscussTools(ItemResolver itemResolver, IWorkItemStore store, ICommentStore comments, StaleWrites writes, CaptureSupport support, ILinkCards linkCards, ICurrentAgent agent)
{
    [McpServerTool(Name = "comment"), McpScope(McpScopes.Write)]
    [Description("Add a comment to a work item. On a Feature this is its session log: add one at the end of every working session. Plain text only; @mentions do not notify anyone yet. It shows as the person's comment, via this agent.")]
    public async Task<CallToolResult> Comment(
        [Description("The item's key, such as SHIP-12.")] string item,
        [Description("The comment, in plain text.")] string text,
        CancellationToken cancellationToken = default)
    {
        var resolved = await itemResolver.ResolveAsync(item, ProjectRole.Member, cancellationToken: cancellationToken);
        var added = await comments.AddAsync(resolved.Id, new CommentCreate(text ?? string.Empty), cancellationToken);
        var project = await support.ProjectOfAsync(resolved, cancellationToken);
        var result = new CommentResult(
            project.Key, McpOpenUrls.WorkItem(await support.BaseUrlAsync(cancellationToken), project.Key, resolved.Key), resolved.Key, added.Id, added.AgentName);
        return CaptureSupport.Reply(result, $"Commented on {resolved.Key}.");
    }

    [McpServerTool(Name = "link"), McpScope(McpScopes.Write)]
    [Description("Link a work item to another one (related, blocked_by, duplicate or predecessor), or to an outside page such as a pull request or a doc by passing url and a title.")]
    public async Task<CallToolResult> Link(
        [Description("The item's key, such as SHIP-12.")] string item,
        [Description("The other item's key. Leave out when you pass a url.")] string? to = null,
        [Description("related (the default), blocked_by (item is blocked by the other), duplicate or predecessor (the other comes first).")] string? type = null,
        [Description("An outside page's address (http or https), instead of to.")] string? url = null,
        [Description("What the outside page is called. Leave out to use the title ShipLean reads from the page.")] string? title = null,
        [Description("A note about why the page is linked, shown on its card.")] string? note = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await itemResolver.ResolveAsync(item, ProjectRole.Member, cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(to) == string.IsNullOrWhiteSpace(url))
        {
            throw ApiException.Validation("Pass either the other item's key (to) or a url, not both and not neither.", "to");
        }

        var project = await support.ProjectOfAsync(resolved, cancellationToken);
        string linkedTo;
        string linkType;
        int revision;
        if (!string.IsNullOrWhiteSpace(url))
        {
            var card = await linkCards.AddUrlAsync(resolved.Id, url, title, note, AgentStamp.Of(agent), cancellationToken: cancellationToken);
            linkedTo = card.Url;
            linkType = "url";
            revision = (await store.GetAsync(resolved.Id, cancellationToken)).Revision;
        }
        else
        {
            var kind = ParseType(type);
            var target = await itemResolver.ResolveAsync(to!, ProjectRole.Member, field: "to", cancellationToken: cancellationToken);
            var snapshot = await store.GetAsync(resolved.Id, cancellationToken);
            var written = await writes.PatchAsync(
                resolved.Id, resolved.Key, new WorkItemChanges { AddLinks = [new LinkValue(kind, target.Id, target.Key, null, null)] }, snapshot.Revision, false, cancellationToken);
            linkedTo = target.Key;
            linkType = Name(kind);
            revision = written.Revision;
        }

        var result = new LinkResult(
            project.Key, McpOpenUrls.WorkItem(await support.BaseUrlAsync(cancellationToken), project.Key, resolved.Key), resolved.Key, linkedTo, linkType, revision);
        return CaptureSupport.Reply(result, $"Linked {resolved.Key} to {linkedTo} ({linkType}).");
    }

    private static LinkType ParseType(string? text)
        => new string((text ?? "related").Where(char.IsLetter).ToArray()).ToLowerInvariant() switch
        {
            "related" => LinkType.Related,
            "blockedby" => LinkType.BlockedBy,
            "duplicate" => LinkType.Duplicate,
            "predecessor" => LinkType.Predecessor,
            _ => throw ApiException.Validation($"\"{text}\" is not a link type: use related, blocked_by, duplicate or predecessor.", "type"),
        };

    private static string Name(LinkType type) => type == LinkType.BlockedBy ? "blocked_by" : type.ToString().ToLowerInvariant();
}
