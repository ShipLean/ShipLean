using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

public sealed record DecideNotNeededInput
{
    [JsonPropertyName("point"), Description("The id of the retro point.")]
    public required Guid Point { get; init; }

    [JsonPropertyName("reason"), Description("Why no action is needed.")]
    public required string Reason { get; init; }
}
