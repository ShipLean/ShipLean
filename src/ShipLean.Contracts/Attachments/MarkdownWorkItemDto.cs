using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Attachments;

// The work item a Markdown file belongs to, as the reader's header shows it.
public sealed record MarkdownWorkItemDto(Guid Id, string Key, string Title, TypeRef Type);
