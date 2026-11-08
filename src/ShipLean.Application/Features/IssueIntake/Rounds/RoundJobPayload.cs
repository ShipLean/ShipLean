using System.Text.Json.Serialization;

namespace ShipLean.Application.Features.IssueIntake.Rounds;

// A round job's payload: which round of the issue it starts.
public sealed record RoundJobPayload([property: JsonPropertyName("round")] int Round);
