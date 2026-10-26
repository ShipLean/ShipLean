using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems.Reads.Changes;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Features.Mcp.Tools.Status;

[McpServerToolType]
internal sealed class ChangesTools(
    ProjectResolver projects,
    IProjectAccess access,
    IWorkItemChanges changes,
    ICurrentUser currentUser,
    IAgentAddresses addresses)
{
    [McpServerTool(Name = "whats_changed_since"), McpScope(McpScopes.Read)]
    [Description("What moved since you last looked: the edits and comments on work items, grouped by item, with who and when, and a new cursor to keep for next time. Give the cursor from your last call, or a time (ISO 8601, such as 2026-10-20T09:00:00Z) the first time. By default only your own items (assigned to you or created by you); mine=false covers the whole project.")]
    public async Task<CallToolResult> WhatsChangedSince(
        [Description("The project key, such as SHIP. Leave out for the most recent project.")] string? project = null,
        [Description("The cursor an earlier call returned.")] string? cursor = null,
        [Description("An ISO 8601 time to start from, when you have no cursor.")] string? since = null,
        [Description("true (the default) for items assigned to or created by you; false for every item in the project.")] bool mine = true,
        [Description("The most changes to return, 1 to 500. Defaults to 100.")] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: false, cancellationToken);
        await access.RequireAsync(resolved.Id, ProjectRole.Member, cancellationToken);
        if (cursor is not null && since is not null)
        {
            throw ApiException.Validation("Give a cursor or a time, not both.", "since");
        }
        if (cursor is null && since is null)
        {
            throw ApiException.Validation("Give the cursor from your last call, or a time to start from.", "since");
        }

        var user = mine ? currentUser.Id : (Guid?)null;
        var page = cursor is not null
            ? await changes.SinceAsync(resolved.Id, cursor, limit, user, cancellationToken)
            : await changes.SinceTimeAsync(resolved.Id, ParseTime(since!), limit, user, cancellationToken);
        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;

        var items = page.Entries
            .GroupBy(entry => entry.WorkItemId)
            .Select(group => new ChangesResult.ChangedItem(group.First().Key, group.First().Title, [.. group.Select(ToLine)]))
            .ToList();
        var result = new ChangesResult(resolved.Key, McpOpenUrls.Backlog(issuer, resolved.Key, "stories"), items, page.Cursor, page.More);
        return StatusReply.Of(result, $"{page.Entries.Count} changes on {items.Count} items in {resolved.Key}{(page.More ? ", more to read" : string.Empty)}.");
    }

    // ISO 8601 only: a date, or a date and time with seconds and fraction optional and the offset (Z or +hh:mm) optional (UTC if left off).
    private static readonly string[] IsoFormats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-dd'T'HH:mmK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
    ];

    private static DateTimeOffset ParseTime(string since)
        => DateTimeOffset.TryParseExact(since.Trim(), IsoFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time)
            ? time
            : throw ApiException.Validation($"\"{since}\" is not a time. Use ISO 8601, such as 2026-10-20T09:00:00Z.", "since");

    private static ChangesResult.ChangeLine ToLine(ChangeEntryDto entry) => new(
        entry.Kind == ChangeKind.Revision ? "revision" : "comment",
        entry.ActorName, entry.At, entry.Revision,
        entry.Fields is null ? null : [.. entry.Fields.Select(Describe)],
        entry.Comment);

    private static string Describe(FieldChange change) => $"{change.Field}: {Show(change.FromLabel, change.From)} -> {Show(change.ToLabel, change.To)}";

    private static string Show(string? label, JsonElement value)
        => label ?? value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => "none",
            JsonValueKind.String => value.GetString() ?? "none",
            _ => value.GetRawText(),
        };
}
