namespace ShipLean.Contracts.Markdown;

// The markdown as sent and the sanitised HTML it renders to. Images lists the images it refers to; without a work item
// every one is classified against no attachments.
public sealed record RenderedMarkdownDto(string Markdown, string Html, IReadOnlyList<MarkdownImageDto> Images);
