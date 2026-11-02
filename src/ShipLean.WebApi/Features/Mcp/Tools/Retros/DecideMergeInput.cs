using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

public sealed record DecideMergeInput
{
    [JsonPropertyName("from"), Description("The theme to fold away.")]
    public required string From { get; init; }

    [JsonPropertyName("into"), Description("The theme to keep.")]
    public required string Into { get; init; }

    [JsonPropertyName("reason"), Description("Why they are the same theme. A person decides; this only proposes.")]
    public required string Reason { get; init; }
}
