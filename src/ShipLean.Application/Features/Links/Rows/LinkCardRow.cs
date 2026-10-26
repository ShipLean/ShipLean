using System.Text.Json;
using ShipLean.Contracts.Links;
using ShipLean.Core;

namespace ShipLean.Application.Features.Links.Rows;

// One live URL link as stored, with its work item's project. Kind, Number and Location are null for a link the story form
// wrote: ToDto reads them from the address then, so every URL link is a card. Title is only what a person typed.
// State, StateSource, StateAsOf and StateReportedBy are the newer of the card's own state and the one a webhook last reported for
// its address (source Webhook then); OwnStateSource is always the card's own, which is what the title rules read.
public sealed record LinkCardRow(
    Guid Id,
    Guid WorkItemId,
    Guid ProjectId,
    string Url,
    string? Title,
    string? Note,
    string? Kind,
    string? PreviewTitle,
    string? PreviewDescription,
    string? PreviewSite,
    string? PreviewImageKey,
    string? PreviewFaviconKey,
    string? PreviewStatus,
    DateTimeOffset? PreviewFetchedAt,
    int? Number,
    string? Location,
    string? Author,
    string? State,
    string? StateSource,
    DateTimeOffset? StateAsOf,
    string? StateReportedBy,
    string? OwnStateSource,
    DateTimeOffset CreatedAt)
{
    // A webhook has reported this address and its report is later than the card's own state (a tie keeps the card's).
    private const string WebhookWins = "(ls.url_key IS NOT NULL AND (l.state_as_of IS NULL OR ls.as_of > l.state_as_of))";

    public const string Columns =
        $"""
        l.id, l.source_id AS workItemId, w.project_id AS projectId, l.url, l.title, l.note, l.kind,
        l.preview_title AS previewTitle, l.preview_description AS previewDescription, l.preview_site AS previewSite,
        l.preview_image_key AS previewImageKey, l.preview_favicon_key AS previewFaviconKey, l.preview_status AS previewStatus,
        l.preview_fetched_at AS previewFetchedAt, l.number, l.location, l.author,
        CASE WHEN {WebhookWins} THEN ls.state ELSE l.state END AS state,
        CASE WHEN {WebhookWins} THEN 'webhook' ELSE l.state_source END AS stateSource,
        CASE WHEN {WebhookWins} THEN ls.as_of ELSE l.state_as_of END AS stateAsOf,
        CASE WHEN {WebhookWins} THEN NULL ELSE l.state_reported_by END AS stateReportedBy,
        l.state_source AS ownStateSource, l.created_at AS createdAt
        """;

    public const string From =
        "FROM work_item_link l JOIN work_item w ON w.id = l.source_id LEFT JOIN link_state ls ON l.link_type = 'url' AND ls.url_key = link_url_key(l.url)";

    // The stored text of an enum value: camelCase, as the CHECK constraints spell it.
    public static string ToDb<T>(T value) where T : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static T? Parse<T>(string? text) where T : struct, Enum => text is null ? null : Enum.Parse<T>(text, ignoreCase: true);

    public LinkAddress Address => LinkAddress.Of(Url);

    public LinkKind KindOrAddress => Parse<LinkKind>(Kind) ?? Address.Kind;

    public LinkState? StateValue => Parse<LinkState>(State);

    public LinkStateSource StateSourceValue => Parse<LinkStateSource>(StateSource) ?? LinkStateSource.None;

    public LinkPreviewStatus PreviewStatusValue => Parse<LinkPreviewStatus>(PreviewStatus) ?? LinkPreviewStatus.None;

    // Typed title, else the title the source read (Live when the provider's API gave it, Fetched when it came from the page), else the address.
    public (string Title, LinkTitleSource Source) TitleAndSource()
    {
        if (!string.IsNullOrWhiteSpace(Title))
        {
            return (Title, LinkTitleSource.User);
        }

        if (!string.IsNullOrWhiteSpace(PreviewTitle))
        {
            return (PreviewTitle, KindOrAddress != LinkKind.Page && Parse<LinkStateSource>(OwnStateSource) == LinkStateSource.Live ? LinkTitleSource.Live : LinkTitleSource.Fetched);
        }

        return (Url, LinkTitleSource.Url);
    }

    public LinkCardDto ToDto(TimeProvider clock, bool refreshDue = false)
    {
        var address = Address;
        var kind = KindOrAddress;
        var (title, titleSource) = TitleAndSource();
        return new LinkCardDto(
            Id, Url, kind, kind == LinkKind.Page ? LinkGroup.Page : LinkGroup.Code, title, titleSource, Note,
            Number ?? address.Number, Location ?? address.Location, PreviewSite ?? address.Host, Author,
            StateValue, StateSourceValue, StateAsOf, StateAsOf is { } asOf ? DisplayDate.Relative(asOf, clock) : null, StateReportedBy,
            PreviewDescription, PreviewImageKey is null ? null : $"/api/link-cards/{Id}/image", PreviewFaviconKey is null ? null : $"/api/link-cards/{Id}/favicon",
            PreviewStatusValue, refreshDue);
    }
}
