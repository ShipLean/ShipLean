using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Asks;

// How a backfilled decision was already answered.
public sealed record SettledArg
{
    [JsonPropertyName("by"), Description("Who answered: me, an email, or a name.")]
    public string? By { get; init; }

    [JsonPropertyName("on"), Description("The day it was answered, yyyy-MM-dd (UTC).")]
    public string? On { get; init; }

    [JsonPropertyName("answer"), Description("recommended, alternative:<n> (1 is the first alternative), or the owner's own words.")]
    public string? Answer { get; init; }
}
