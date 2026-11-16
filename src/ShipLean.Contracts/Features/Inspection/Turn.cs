namespace ShipLean.Contracts.Inspection;

// PromptText is always filled when present; hiding it is the views' job.
public sealed record Turn(int Number, DateTimeOffset Start, DateTimeOffset End, string? PromptId, string? PromptText, IReadOnlyList<RunRow> Rows);
