namespace ShipLean.Contracts.Markdown;

// A heading: Level 1-6, its plain text, and the id the rendered heading carries (link to it as "#{Anchor}").
public sealed record MarkdownOutlineEntryDto(int Level, string Text, string Anchor);
