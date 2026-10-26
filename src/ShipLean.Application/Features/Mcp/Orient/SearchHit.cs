namespace ShipLean.Application.Features.Mcp.Orient;

// Score is the number of the searched words the title shares (WordOverlap); 0 when only the description matched or there were no words.
// AttachmentFile names a live Markdown attachment whose text holds one of the words, null when none does.
public sealed record SearchHit(Guid Id, string Key, string Title, string Type, string State, string? Assignee, string? Sprint, decimal? Points, string? Parent, IReadOnlyList<string> Tags, int Score, string? AttachmentFile = null);
