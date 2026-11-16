using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Code;

// What add_commits answers: Added are the commits now on the item, Skipped the ones it already had. Sha is the full sha ShipLean resolved.
public sealed record AddCommitsResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("item")] string Item,
    [property: JsonPropertyName("added")] IReadOnlyList<AddedCommit> Added,
    [property: JsonPropertyName("skipped")] IReadOnlyList<AddedCommit> Skipped) : McpResult(Project, OpenUrl);
