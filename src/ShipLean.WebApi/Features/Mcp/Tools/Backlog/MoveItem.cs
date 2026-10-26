using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Backlog;

// One item move_to_sprint is asked to move.
public sealed record MoveItem
{
    [JsonPropertyName("item"), Description("The work item key, such as SHIP-123.")]
    public string? Item { get; init; }

    [JsonPropertyName("expected_revision"), Description("The revision you last saw (from get_work_item). Leave out to move whatever is there now.")]
    public int? ExpectedRevision { get; init; }
}
