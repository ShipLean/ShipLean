using System.Text.Json;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core;

namespace ShipLean.Application.Features.WorkItems;

// One live work_item_link row with its target's key and title, and for a url link the state a webhook last reported for
// it (link_state) and, when a webhook added it, why.
internal sealed record WorkItemLinkRow(
    Guid Id,
    Guid SourceId,
    string TypeText,
    Guid? TargetId,
    string? TargetKey,
    string? TargetTitle,
    string? Url,
    string? Title,
    string? AddedVia,
    string? MatchedOn,
    string? CardState,
    string? CardTitle,
    DateTimeOffset? CardAsOf,
    string? CardSource,
    bool Incoming)
{
    // Two parts in one query: the links each item owns, and (Incoming) the Caused by links other items made to it, which read as
    // rows of the item they point at (sourceId = the Task, targetId = the Bug) so a Task's read can list the Bugs it caused.
    public const string Select =
        """
        SELECT * FROM (
            SELECT l.id, l.source_id AS sourceId, l.link_type AS typeText, l.target_id AS targetId, t.key AS targetKey,
                   t.title AS targetTitle, l.url, l.title, l.added_via AS addedVia, l.matched_on AS matchedOn,
                   ls.state AS cardState, ls.title AS cardTitle, ls.as_of AS cardAsOf, ls.source AS cardSource, false AS incoming, l.created_at AS createdAt
            FROM work_item_link l
            LEFT JOIN work_item t ON t.id = l.target_id
            LEFT JOIN link_state ls ON l.link_type = 'url' AND ls.url_key = link_url_key(l.url)
            WHERE l.source_id = ANY(@ids) AND l.archived_at IS NULL
            UNION ALL
            SELECT l.id, l.target_id, l.link_type, l.source_id, b.key, b.title, NULL, NULL, NULL, NULL,
                   NULL, NULL, NULL::timestamptz, NULL, true, l.created_at
            FROM work_item_link l
            JOIN work_item b ON b.id = l.source_id
            WHERE l.target_id = ANY(@ids) AND l.link_type = 'causedBy' AND l.archived_at IS NULL
        ) x
        ORDER BY x.createdAt, x.id
        """;

    public static string TypeToDb(LinkType type) => JsonNamingPolicy.CamelCase.ConvertName(type.ToString());

    public LinkType Type => Enum.Parse<LinkType>(TypeText, ignoreCase: true);

    public LinkSnapshot ToSnapshot() => new(Id, Type == LinkType.Url
        ? new LinkValue(Type, null, null, Url, Title)
        : new LinkValue(Type, TargetId, TargetKey, null, null));

    public LinkDto ToDto(DateTimeOffset now, TimeZoneInfo zone) => new(
        Id,
        Type,
        TargetId,
        TargetKey,
        TargetId is null ? Title : TargetTitle,
        Url,
        CardState is not null && CardAsOf is { } asOf && CardSource is not null
            ? new LinkCardDto(Enum.Parse<LinkCardState>(CardState, ignoreCase: true), CardTitle, asOf, DisplayDate.Relative(asOf, now, zone), Enum.Parse<WebhookProvider>(CardSource, ignoreCase: true))
            : null,
        AddedVia is not null && MatchedOn is not null
            ? new AutoLinkDto(Enum.Parse<WebhookProvider>(AddedVia, ignoreCase: true), Enum.Parse<AutoLinkMatch>(MatchedOn, ignoreCase: true))
            : null);
}
