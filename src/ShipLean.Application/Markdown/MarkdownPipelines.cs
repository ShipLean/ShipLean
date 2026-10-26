using Markdig;

namespace ShipLean.Application.Markdown;

// The one Markdig pipeline: the renderer, the heading outline and the image list must parse alike, or the ids and images they report would not match the HTML.
internal static class MarkdownPipelines
{
    public static MarkdownPipeline Shared { get; } = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .UseTaskLists()
        .UseAutoLinks()
        .UseAutoIdentifiers()
        .Build();

    // Markdig refuses input nested past its depth limit (a few hundred quote marks or emphasis markers).
    public static Markdig.Syntax.MarkdownDocument Parse(string markdown)
    {
        try
        {
            return Markdig.Markdown.Parse(markdown, Shared);
        }
        catch (ArgumentException)
        {
            throw Contracts.Errors.ApiException.Validation("Markdown is nested too deeply.");
        }
    }
}
