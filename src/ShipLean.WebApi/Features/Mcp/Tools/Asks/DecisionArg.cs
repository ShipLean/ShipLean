using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Asks;

// One decision an agent asks record_decisions to record.
public sealed record DecisionArg
{
    [JsonPropertyName("number"), Description("The decision's number (3 for D3). Leave out to take the next one. An open number is edited; a settled or withdrawn number is refused.")]
    public int? Number { get; init; }

    [JsonPropertyName("question"), Description("What was decided, as a question.")]
    public string? Question { get; init; }

    [JsonPropertyName("recommended"), Description("The answer you are going with.")]
    public string? Recommended { get; init; }

    [JsonPropertyName("alternatives"), Description("The other answers the owner could pick.")]
    public IReadOnlyList<string>? Alternatives { get; init; }

    [JsonPropertyName("settled"), Description("Only when backfilling a decision that was already answered: who, when and the answer.")]
    public SettledArg? Settled { get; init; }
}
