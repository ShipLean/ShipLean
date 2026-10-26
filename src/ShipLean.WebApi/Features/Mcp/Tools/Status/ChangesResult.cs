using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Status;

// Items with their changes, oldest change first within an item and items in the order of their first change. Cursor is
// what to pass next time; More says the limit cut the page short (call again with the cursor).
internal sealed record ChangesResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("items")] IReadOnlyList<ChangesResult.ChangedItem> Items,
    [property: JsonPropertyName("cursor")] string? Cursor,
    [property: JsonPropertyName("more")] bool More) : McpResult(Project, OpenUrl)
{
    internal sealed record ChangedItem(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("changes")] IReadOnlyList<ChangeLine> Changes);

    // Kind is revision or comment. Fields lists "field: from -> to" for a revision; Comment is the markdown body of a comment.
    internal sealed record ChangeLine(
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("by")] string? By,
        [property: JsonPropertyName("at")] DateTimeOffset At,
        [property: JsonPropertyName("revision")] int? Revision,
        [property: JsonPropertyName("fields")] IReadOnlyList<string>? Fields,
        [property: JsonPropertyName("comment")] string? Comment);
}
