using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.Links;

// The one writer and reader of a work item's links, behind the link endpoints and the MCP link tool. Callers check
// IProjectAccess first; this trusts them. Adds, edits and archives publish linksChanged after the write.
public interface ILinkCards
{
    // The item's live links. Throws NotFound for an unknown work item.
    Task<LinkCardsDto> ListAsync(Guid workItemId, CancellationToken cancellationToken = default);

    // Saves nothing. An unreachable host previews with PreviewStatus Unreachable, it does not throw.
    Task<LinkPreviewDto> PreviewAsync(Guid workItemId, string url, CancellationToken cancellationToken = default);

    // Throws Validation (field "url") for a bad address and Conflict for a URL already linked on the item. AgentStamp is the
    // acting agent's AgentStamp.Of(...) text, null for a person. readPreview false saves the link without reading the address
    // now (for a caller adding many links in one request); the card is then due, and a refresh reads it.
    Task<LinkCardDto> AddUrlAsync(Guid workItemId, string url, string? title, string? note, string? agentStamp, bool readPreview = true, CancellationToken cancellationToken = default);

    // Throws NotFound for an unknown key, Validation for Url, a link to itself or another project's item, Conflict for a repeat.
    Task<WorkItemLinkRowDto> AddWorkItemLinkAsync(Guid workItemId, string targetKey, LinkType type, CancellationToken cancellationToken = default);

    // URL links only (a work item link has no title or note). Null leaves a field as it is; empty clears it. Throws NotFound for an unknown or archived link.
    Task<LinkCardDto> EditAsync(Guid linkId, string? title, string? note, CancellationToken cancellationToken = default);

    // URL links and work item links alike; the row stays. Throws NotFound for an unknown or archived link.
    Task ArchiveAsync(Guid linkId, CancellationToken cancellationToken = default);
}
